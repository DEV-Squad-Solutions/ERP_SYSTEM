using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class FinancialStatementServiceTests
{
    static FinancialStatementServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task CashboxStatementCalculatesPeriodOpeningAndClosingBalances()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);

        await vouchers.AddAsync(
            CreateGeneralVoucher(
                "CV-BEFORE",
                new DateOnly(2026, 7, 10),
                CashDirection.Receipt,
                3,
                100m));
        await vouchers.AddAsync(
            CreateGeneralVoucher(
                "CV-PERIOD-PAY",
                new DateOnly(2026, 7, 22),
                CashDirection.Payment,
                4,
                40m));
        await vouchers.AddAsync(
            CreateGeneralVoucher(
                "CV-PERIOD-RECEIPT",
                new DateOnly(2026, 7, 23),
                CashDirection.Receipt,
                3,
                25m));

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(
                    1,
                    FromDate: new DateOnly(2026, 7, 20),
                    ToDate: new DateOnly(2026, 7, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CashboxId);
        Assert.Equal("Main Cashbox", result.Value.CashboxName);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(1100m, result.Value.Summary.OpeningBalance);
        Assert.Equal(25m, result.Value.Summary.TotalReceipts);
        Assert.Equal(40m, result.Value.Summary.TotalPayments);
        Assert.Equal(1085m, result.Value.Summary.ClosingBalance);
        Assert.Equal(
            [1060m, 1085m],
            result.Value.Items.Select(item => item.Balance).ToArray());
    }

    [Fact]
    public async Task PartnerStatementCombinesOpeningInvoiceAndVoucherOnce()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var voucher = await database.CreateVoucherService(1).AddAsync(
            CreatePartnerVoucher(
                "CV-PARTNER-STMT",
                new DateOnly(2026, 7, 15),
                CashDirection.Receipt,
                movementTypeId: 1,
                amount: 50m));

        var result = await database.CreateStatementService(1)
            .GetPartnerStatementAsync(
                Page(),
                new PartnerStatementFilterRequest(1));

        Assert.True(voucher.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BusinessPartnerId);
        Assert.Equal("Customer One", result.Value.BusinessPartnerName);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Single(
            result.Value.Items,
            item =>
                item.MovementName == "سند قبض" &&
                item.DebitAmount == 0m &&
                item.CreditAmount == 50m);
        Assert.Equal(250m, result.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "عليه",
            result.Value.Summary.ClosingBalanceDescription);
        Assert.All(
            result.Value.Items,
            item => Assert.False(string.IsNullOrWhiteSpace(
                item.MovementName)));
    }

    [Fact]
    public async Task DriverStatementIncludesGeneralLinkedAndTripCostRows()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(1);
        await vouchers.AddAsync(
            CreateDriverVoucher(
                "CV-DRIVER-GENERAL",
                new DateOnly(2026, 7, 18),
                CashDirection.Payment,
                tripId: null,
                amount: 100m));
        await vouchers.AddAsync(
            CreateDriverVoucher(
                "CV-DRIVER-LINKED",
                new DateOnly(2026, 7, 21),
                CashDirection.Receipt,
                tripId: 1,
                amount: 20m));

        var trips = database.CreateDriverTripService(1);
        var trip = Assert.Single(
            (await trips.GetCostEntryAsync(
                Page(),
                new DriverTripCostFilterRequest(TripNumber: "TR-1")))
            .Value.Items);
        await trips.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    60m,
                    "Trip cost",
                    trip.RowVersion)
            ]));

        var statements = database.CreateStatementService(1);
        var overall = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(1));
        var tripSpecific = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(
                1,
                DriverTripId: 1));

        Assert.True(overall.IsSuccess);
        Assert.Equal(1, overall.Value.DriverId);
        Assert.Equal("Driver One", overall.Value.DriverName);
        Assert.Equal(3, overall.Value.TotalCount);
        Assert.Equal(100m, overall.Value.Summary.TotalPaidToDriver);
        Assert.Equal(20m, overall.Value.Summary.TotalReceivedFromDriver);
        Assert.Equal(60m, overall.Value.Summary.TotalTripCost);
        Assert.Equal(20m, overall.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "مبلغ مطلوب من السائق",
            overall.Value.Summary.ClosingBalanceDescription);

        Assert.Equal(2, tripSpecific.Value.TotalCount);
        Assert.Equal(0m, tripSpecific.Value.Summary.TotalPaidToDriver);
        Assert.Equal(20m, tripSpecific.Value.Summary.TotalReceivedFromDriver);
        Assert.Equal(60m, tripSpecific.Value.Summary.TotalTripCost);
        Assert.Equal(80m, tripSpecific.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "مبلغ مطلوب دفعه للسائق",
            tripSpecific.Value.Summary.ClosingBalanceDescription);
        Assert.Contains(
            overall.Value.Items,
            item =>
                item.SourceName == "رحلة سائق" &&
                item.MovementName == "تكلفة رحلة");
    }

    [Fact]
    public async Task DeletedVoucherIsExcludedFromEveryStatement()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(1);
        var created = await vouchers.AddAsync(
            CreateDriverVoucher(
                "CV-DELETED",
                new DateOnly(2026, 7, 25),
                CashDirection.Payment,
                tripId: null,
                amount: 100m));
        database.Context.ChangeTracker.Clear();
        await vouchers.DeleteAsync(created.Value.Id);

        var statements = database.CreateStatementService(1);
        var cashbox = await statements.GetCashboxStatementAsync(
            Page(),
            new CashboxStatementFilterRequest(
                1,
                Search: "CV-DELETED"));
        var driver = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(
                1,
                Search: "CV-DELETED"));

        Assert.Empty(cashbox.Value.Items);
        Assert.Empty(driver.Value.Items);
        Assert.Equal(1000m, cashbox.Value.Summary.ClosingBalance);
    }

    private static PaginationRequest Page() =>
        new()
        {
            PageNumber = 1,
            PageSize = 20
        };

    private static CashVoucherRequest CreateGeneralVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int movementTypeId,
        decimal amount) =>
        new(
            number,
            date,
            direction,
            1,
            movementTypeId,
            CashPartyType.None,
            null,
            null,
            null,
            null,
            amount,
            null,
            number,
            null);

    private static CashVoucherRequest CreatePartnerVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int movementTypeId,
        decimal amount) =>
        new(
            number,
            date,
            direction,
            1,
            movementTypeId,
            CashPartyType.Partner,
            1,
            null,
            null,
            null,
            amount,
            "REF-PARTNER",
            number,
            null);

    private static CashVoucherRequest CreateDriverVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int? tripId,
        decimal amount) =>
        new(
            number,
            date,
            direction,
            1,
            direction == CashDirection.Payment ? 4 : 3,
            CashPartyType.Driver,
            null,
            1,
            tripId,
            null,
            amount,
            "REF-DRIVER",
            number,
            null);
}
