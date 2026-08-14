using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Persistence;

public sealed class ModelConfigurationTests
{
    [Fact]
    public void InventoryCostAllocation_HasMatchingCompanyQueryFilter()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(
            typeof(InventoryCostAllocation));

        Assert.NotNull(entityType);
        Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
    }

    [Fact]
    public void CompanySettings_BaseCurrency_UsesEgpAsDefaultAndSentinel()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(CompanySettings));
        var property = entityType?.FindProperty(
            nameof(CompanySettings.BaseCurrency));

        Assert.NotNull(property);
        Assert.Equal(CurrencyCode.EGP, property.Sentinel);
        Assert.Equal(CurrencyCode.EGP, property.GetDefaultValue());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=MiniErpModelTests;Trusted_Connection=True")
            .Options;
        return new ApplicationDbContext(options);
    }
}
