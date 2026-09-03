using System.Text.Json.Serialization;
using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Errors;
using MiniErp.Api.Exceptions;
using MiniErp.Api.ModelBinding;
using MiniErp.Api.Realtime;
using MiniErp.Api.Swagger;
using MiniErp.Api.Startup;
using MiniErp.Api.Validation;
using MiniErp.Application;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Validation;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

const string AllowAnyFrontendPolicy = "AllowAnyFrontend";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);
ArabicValidationConfiguration.Configure();

builder.Services
    .AddControllers(options =>
        options.ModelBinderProviders.Insert(
            0,
            new FlexibleDateOnlyModelBinderProvider()))
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                namingPolicy: null,
                allowIntegerValues: true)));
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        AllowAnyFrontendPolicy,
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<StartupDatabaseStatus>();
builder.Services.AddSingleton<StartupDatabaseInitializer>();
builder.Services.AddHostedService<DatabaseRecoveryService>();
var hangfireConnectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        hangfireConnectionString,
        new SqlServerStorageOptions
        {
            // Hangfire owns its infrastructure schema separately from the
            // application EF migrations. Create/update it when the server
            // starts, even when application migrations are disabled.
            PrepareSchemaIfNecessary = true,
            TryAutoDetectSchemaDependentOptions = false,
            DisableGlobalLocks = true,
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true
        }));
builder.Services.AddHostedService<DeferredHangfireServer>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = true;
    options.InvalidModelStateResponseFactory = context =>
    {
        var response = ApiErrorResponseFactory.Validation(
            context.HttpContext,
            context.ModelState);
        return ApiErrorResponseFactory.ToObjectResult(response);
    };
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(
    ServiceLifetime.Scoped);
builder.Services.AddFluentValidationAutoValidation(configuration =>
{
    configuration.DisableBuiltInModelValidation = true;
    configuration.OverrideDefaultResultFactoryWith<
        ArabicValidationResultFactory>();
});
builder.Services.Scan(scan => scan
    .FromAssemblies(
        typeof(ApplicationAssemblyMarker).Assembly,
        typeof(InfrastructureAssemblyMarker).Assembly)
    .AddClasses(classes => classes.AssignableTo<IScopedService>())
    .AsMatchingInterface()
    .WithScopedLifetime());
builder.Services.AddSwaggerDocumentation(builder.Configuration);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = ApiErrorResponseFactory.FromStatusCode(
        context.HttpContext,
        context.HttpContext.Response.StatusCode);
    await ApiErrorResponseFactory.WriteAsync(
        context.HttpContext,
        response);
});
app.UseSwaggerDocumentation();

app.UseHttpsRedirection();
app.UseCors(AllowAnyFrontendPolicy);
app.UseMiddleware<DatabaseReadinessMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<UpdatesHub>("/hubs/updates");
app.MapGet(
        "/health/live",
        () => Results.Ok(new HealthStatusResponse("Live")))
    .AllowAnonymous()
    .WithName("Health_Live")
    .WithTags("Health")
    .WithSummary("فحص تشغيل الـAPI")
    .WithDescription(
        "يرجع 200 عندما تكون عملية الـAPI نفسها تعمل. لا يفحص اتصال قاعدة البيانات.")
    .Produces<HealthStatusResponse>(StatusCodes.Status200OK);
app.MapGet(
        "/health/ready",
        (StartupDatabaseStatus status) => DatabaseHealthResult(status))
    .AllowAnonymous()
    .WithName("Health_Ready")
    .WithTags("Health")
    .WithSummary("فحص جاهزية التطبيق")
    .WithDescription(
        "يرجع 200 عند جاهزية قاعدة البيانات، أو 503 أثناء التعافي أو فشل الاتصال.")
    .Produces<HealthStatusResponse>(StatusCodes.Status200OK)
    .Produces<HealthStatusResponse>(StatusCodes.Status503ServiceUnavailable);
app.MapGet(
        "/health/database",
        (StartupDatabaseStatus status) => DatabaseHealthResult(status))
    .AllowAnonymous()
    .WithName("Health_Database")
    .WithTags("Health")
    .WithSummary("فحص اتصال قاعدة البيانات")
    .WithDescription(
        "يعرض حالة تهيئة واتصال قاعدة البيانات الحالية؛ يرجع 503 عندما لا تكون جاهزة.")
    .Produces<HealthStatusResponse>(StatusCodes.Status200OK)
    .Produces<HealthStatusResponse>(StatusCodes.Status503ServiceUnavailable);

app.Run();

static IResult DatabaseHealthResult(StartupDatabaseStatus status)
{
    var snapshot = status.GetSnapshot();
    return snapshot.IsReady
        ? Results.Ok(new HealthStatusResponse(snapshot.State))
        : Results.Json(
            new HealthStatusResponse(snapshot.State),
            statusCode: StatusCodes.Status503ServiceUnavailable);
}

public sealed record HealthStatusResponse(string Status);
