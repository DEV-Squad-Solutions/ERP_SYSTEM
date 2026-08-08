using System.Text.Json.Serialization;
using FluentValidation;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Errors;
using MiniErp.Api.Exceptions;
using MiniErp.Api.ModelBinding;
using MiniErp.Api.Realtime;
using MiniErp.Api.Swagger;
using MiniErp.Api.Validation;
using MiniErp.Application;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Validation;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Seeding;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

const string AllowAnyFrontendPolicy = "AllowAnyFrontend";

var builder = WebApplication.CreateBuilder(args);

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
                allowIntegerValues: false)));
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
builder.Services.AddHostedService<RealtimeOutboxDispatcher>();
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
builder.Services.AddScoped<
    IExchangeRateResolver,
    MiniErp.Infrastructure.Services.ExchangeRates.ExchangeRateService>();
builder.Services.AddSwaggerDocumentation(builder.Configuration);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    await app.ApplyPendingMigrationsAsync();
}

if (app.Configuration.GetValue("Seed:Enabled", false))
{
    await DevelopmentDataSeeder.SeedAsync(app.Services, app.Configuration);
}

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<UpdatesHub>("/hubs/updates");

app.Run();
