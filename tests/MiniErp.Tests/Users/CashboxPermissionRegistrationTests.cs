using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Api.Controllers;
using MiniErp.Application;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Services.Users;

namespace MiniErp.Tests.Users;

public sealed class CashboxPermissionRegistrationTests
{
    [Fact]
    public void ProductionServiceScan_InjectsPermissionResolverIntoFinancialServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Scan(scan => scan
            .FromAssemblies(typeof(ApplicationAssemblyMarker).Assembly, typeof(InfrastructureAssemblyMarker).Assembly)
            .AddClasses(classes => classes.AssignableTo<IScopedService>())
            .AsMatchingInterface().WithScopedLifetime());
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=MiniErpTests",
            ["Jwt:Issuer"] = "MiniErp.Tests",
            ["Jwt:SigningKey"] = "MiniErp-tests-signing-key-at-least-32-bytes",
            ["Jwt:AccessToken:Audience"] = "MiniErp.Tests.Client",
            ["Jwt:AccessToken:ExpirationMinutes"] = "15",
            ["Jwt:CompanySelectionToken:Audience"] = "MiniErp.Tests.CompanySelection",
            ["Jwt:CompanySelectionToken:ExpirationMinutes"] = "5",
            ["Jwt:RefreshToken:ExpirationDays"] = "1"
        }).Build());
        services.AddScoped<ICurrentCompanyContext>(_ => new CompanyContext());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<ICashboxAccessService>();
        Assert.IsType<CashboxAccessService>(access);
        var financialServices = typeof(InfrastructureAssemblyMarker).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(ICashboxAccessService))))
            .ToArray();
        Assert.Contains(financialServices, type => type.Name == "CashboxService");
        Assert.Contains(financialServices, type => type.Name == "CashVoucherService");
        Assert.Contains(financialServices, type => type.Name == "CashboxTransferService");
        Assert.Contains(financialServices, type => type.Name == "InvoiceService");
        foreach (var type in financialServices)
        {
            var contract = type.GetInterfaces().Single(item => item.Name == $"I{type.Name}");
            var instance = scope.ServiceProvider.GetRequiredService(contract);
            var capturedAccess = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(ICashboxAccessService));
            Assert.Same(access, capturedAccess.GetValue(instance));
        }
    }

    [Fact]
    public void PermissionManagement_UsesOnlyUserScopedRoutes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddApplicationPart(typeof(UserCashboxPermissionsController).Assembly);
        using var provider = services.BuildServiceProvider();
        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UserCashboxPermissionsController))
            .ToArray();
        Assert.Equal(2, actions.Length);
        Assert.All(actions, action => Assert.Equal(
            "api/v{version:apiVersion}/Users/{userId:guid}/cashbox-permissions", action.AttributeRouteInfo!.Template));
    }

    private sealed class CompanyContext : ICurrentCompanyContext { public int CompanyId => 1; }
}
