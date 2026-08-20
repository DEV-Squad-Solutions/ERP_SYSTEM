using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class DriverTripCostServiceTests
{
    static DriverTripCostServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task CostEntryQuerySupportsSearchAndHasCostFilters()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateDriverTripService(companyId: 1);

        var withoutCost = await service.GetCostEntryAsync(
            Page(),
            new DriverTripCostFilterRequest(
                Search: "INV-1",
                HasCost: false));
        var withCost = await service.GetCostEntryAsync(
            Page(),
            new DriverTripCostFilterRequest(
                TripNumber: "TR-2",
                HasCost: true));

        var withoutCostRow = Assert.Single(withoutCost.Value.Items);
        Assert.Equal(1, withoutCostRow.DriverTripId);
        Assert.Equal(1, withoutCostRow.BusinessPartnerId);
        Assert.Equal("Customer One", withoutCostRow.BusinessPartnerName);
        Assert.Equal("Egypt", withoutCostRow.CountryName);
        var withCostRow = Assert.Single(withCost.Value.Items);
        Assert.Equal(2, withCostRow.DriverTripId);
        Assert.Null(withCostRow.CountryName);
    }

    [Fact]
    public async Task BulkUpdateChangesEveryValidRowAtomically()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateDriverTripService(companyId: 1);
        var rows = await service.GetCostEntryAsync(Page());
        var byId = rows.Value.Items.ToDictionary(item => item.DriverTripId);
        var beforeVoucherCount =
            await database.Context.CashVouchers.CountAsync();
        var beforeMovementCount =
            await database.Context.BusinessPartnerMovements.CountAsync();
        var invoiceNumbers = await database.Context.Invoices
            .OrderBy(item => item.Id)
            .Select(item => item.InvoiceNumber)
            .ToListAsync();

        var result = await service.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    120m,
                    "Road and fuel",
                    byId[1].RowVersion),
                new DriverTripCostUpdateItem(
                    2,
                    null,
                    null,
                    byId[2].RowVersion)
            ]));

        Assert.True(result.IsSuccess);
        var updatedFirstTrip = result.Value.Items.Single(
            item => item.DriverTripId == 1);
        Assert.Equal(120m, updatedFirstTrip.Cost);
        Assert.Equal(1, updatedFirstTrip.BusinessPartnerId);
        Assert.Equal("Customer One", updatedFirstTrip.BusinessPartnerName);
        Assert.Null(result.Value.Items.Single(
            item => item.DriverTripId == 2).Cost);
        Assert.Equal(
            beforeVoucherCount,
            await database.Context.CashVouchers.CountAsync());
        Assert.Equal(
            beforeMovementCount,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Equal(
            invoiceNumbers,
            await database.Context.Invoices
                .OrderBy(item => item.Id)
                .Select(item => item.InvoiceNumber)
                .ToListAsync());
    }

    [Fact]
    public async Task InvalidOrCrossCompanyRowPreventsAllUpdates()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateDriverTripService(companyId: 1);
        var rows = await service.GetCostEntryAsync(Page());
        var tripOne = rows.Value.Items.Single(item => item.DriverTripId == 1);

        var result = await service.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    99m,
                    null,
                    tripOne.RowVersion),
                new DriverTripCostUpdateItem(
                    3,
                    88m,
                    null,
                    new byte[8])
            ]));

        Assert.Equal("DriverTrips.NotFound", result.Error.Code);
        Assert.Null(
            await database.Context.DriverTrips
                .Where(item => item.Id == 1)
                .Select(item => item.Cost)
                .SingleAsync());
    }

    [Fact]
    public async Task DuplicateIdsAreRejectedBeforeAnyWrite()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateDriverTripService(companyId: 1);
        var row = Assert.Single(
            (await service.GetCostEntryAsync(
                Page(),
                new DriverTripCostFilterRequest(TripNumber: "TR-1")))
            .Value.Items);

        var result = await service.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    10m,
                    null,
                    row.RowVersion),
                new DriverTripCostUpdateItem(
                    1,
                    20m,
                    null,
                    row.RowVersion)
            ]));

        Assert.Equal("DriverTrips.DuplicateIds", result.Error.Code);
        Assert.Null(
            await database.Context.DriverTrips
                .Where(item => item.Id == 1)
                .Select(item => item.Cost)
                .SingleAsync());
    }

    [Fact]
    public async Task StaleTripTokenRejectsWholeBulkRequest()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var original = Assert.Single(
            (await database.CreateDriverTripService(1).GetCostEntryAsync(
                Page(),
                new DriverTripCostFilterRequest(TripNumber: "TR-1")))
            .Value.Items);

        await using var winnerContext = database.CreateAdditionalContext();
        await using var staleContext = database.CreateAdditionalContext();
        var winner = database.CreateDriverTripService(1, winnerContext);
        var stale = database.CreateDriverTripService(1, staleContext);

        var winnerResult = await winner.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    50m,
                    null,
                    original.RowVersion)
            ]));
        var staleResult = await stale.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    60m,
                    null,
                    original.RowVersion)
            ]));

        Assert.True(winnerResult.IsSuccess);
        Assert.Equal("DriverTrips.Concurrency", staleResult.Error.Code);
        Assert.Equal(
            50m,
            await database.Context.DriverTrips
                .Where(item => item.Id == 1)
                .Select(item => item.Cost)
                .SingleAsync());
    }

    private static PaginationRequest Page() =>
        new()
        {
            PageNumber = 1,
            PageSize = 20
        };
}
