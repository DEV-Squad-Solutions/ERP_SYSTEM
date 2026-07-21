using System.Text.Json.Serialization;
using FluentValidation;
using Asp.Versioning;
using MiniErp.Api.Exceptions;
using MiniErp.Api.Swagger;
using MiniErp.Application;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Seeding;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

const string AllowAnyFrontendPolicy = "AllowAnyFrontend";

var builder = WebApplication.CreateBuilder(args);

MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

builder.Services
    .AddControllers()
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

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(
    ServiceLifetime.Scoped);
builder.Services.AddFluentValidationAutoValidation(configuration =>
    configuration.DisableBuiltInModelValidation = true);
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

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    await app.ApplyPendingMigrationsAsync();
}

if (app.Configuration.GetValue("Seed:Enabled", false))
{
    await DevelopmentDataSeeder.SeedAsync(app.Services, app.Configuration);
}

app.UseExceptionHandler();
app.UseSwaggerDocumentation();

app.UseHttpsRedirection();
app.UseCors(AllowAnyFrontendPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
