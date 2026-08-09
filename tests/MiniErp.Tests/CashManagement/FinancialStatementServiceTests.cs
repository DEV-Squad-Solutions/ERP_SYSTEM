using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
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

        await AddVoucherAsync(vouchers,
            CreateGeneralVoucher(
                "CV-BEFORE",
                new DateOnly(2026, 7, 10),
                CashDirection.Receipt,
                3,
                100m));
        await AddVoucherAsync(vouchers,
            CreateGeneralVoucher(
                "CV-PERIOD-PAY",
                new DateOnly(2026, 7, 22),
                CashDirection.Payment,
                4,
                40m));
        await AddVoucherAsync(vouchers,
            CreateGeneralVoucher(
                "CV-PERIOD-RECEIPT",
                new DateOnly(2026, 7, 23),
                CashDirection.Receipt,
                3,
                25m));
        await vouchers.AddAsync(
            new CashVoucherRequest(
                new DateOnly(2026, 7, 24),
                CashDirection.Receipt,
                CashboxId: 1,
                Amount: 500m,
                Description: "Draft excluded from the statement"));

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
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(
            [1060m, 1085m],
            result.Value.Items.Select(item => item.Balance).ToArray());
    }

    [Fact]
    public async Task ForeignCurrencyCashboxStatementReturnsAllCurrencyDetails()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE Cashboxes
            SET OpeningBalanceDate = '2026-07-01',
                OpeningExchangeRate = 48,
                BaseOpeningBalance = 4800
            WHERE Id = 5;

            INSERT INTO CashVouchers (
                CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, PartyType, Amount,
                Currency, ExchangeRate, BaseAmount, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                1, 'USD-RECEIPT', '2026-07-10', 1,
                5, 3, 1, 10,
                2, 50, 500, '2026-07-10',
                'test', '2026-07-10', 'test', 0);
            """);

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(CashboxId: 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.False(result.Value.IsBaseCurrency);
        Assert.Equal(
            new DateOnly(2026, 7, 1),
            result.Value.OpeningBalanceDate);
        Assert.Equal(48m, result.Value.OpeningExchangeRate);
        Assert.Equal(4800m, result.Value.Summary.BaseOpeningBalance);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(CurrencyCode.USD, item.Currency);
        Assert.Equal(CurrencyCode.EGP, item.BaseCurrency);
        Assert.Equal(50m, item.ExchangeRate);
        Assert.False(item.IsBaseCurrency);
        Assert.Equal(10m, item.ReceiptAmount);
        Assert.Equal(500m, item.BaseReceiptAmount);
        Assert.Equal(110m, item.Balance);
        Assert.Equal(5300m, item.BaseBalance);
    }

    [Fact]
    public async Task PartnerStatementCombinesOpeningInvoiceAndVoucherOnce()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var voucher = await AddVoucherAsync(
            database.CreateVoucherService(1),
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
    public async Task ForeignCurrencyPartnerStatementReturnsOriginalAndEgpValues()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE BusinessPartners SET Currency = 2 WHERE Id = 2;

            INSERT INTO PartnerOpeningBalances (
                Id, CompanyId, BusinessPartnerId, DocumentNumber,
                DocumentDate, Currency, ExchangeRate, BalanceType,
                Amount, BaseAmount, Notes,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                2, 1, 2, 'OPEN-USD', '2026-07-01', 2, 50, 1,
                100, 5000, 'USD opening',
                'test', '2026-07-01', 'test', 0);

            INSERT INTO BusinessPartnerMovements (
                Id, CompanyId, BusinessPartnerId, InvoiceId,
                CashVoucherId, MovementType, MovementDate, Currency,
                Debit, Credit, ExchangeRate, BaseDebit, BaseCredit,
                Description, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES (
                2, 1, 2, 2, NULL, 1, '2026-07-10', 2,
                20, 0, 51, 1020, 0,
                'USD invoice', 'test', '2026-07-10', 'test', 0);
            """);

        var result = await database.CreateStatementService(1)
            .GetPartnerStatementAsync(
                Page(),
                new PartnerStatementFilterRequest(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Collection(
            result.Value.Items,
            opening =>
            {
                Assert.Equal(100m, opening.DebitAmount);
                Assert.Equal(50m, opening.ExchangeRate);
                Assert.Equal(5000m, opening.BaseDebitAmount);
                Assert.Equal(100m, opening.BalanceAmount);
                Assert.Equal(5000m, opening.BaseBalanceAmount);
            },
            invoice =>
            {
                Assert.Equal(20m, invoice.DebitAmount);
                Assert.Equal(51m, invoice.ExchangeRate);
                Assert.Equal(1020m, invoice.BaseDebitAmount);
                Assert.Equal(120m, invoice.BalanceAmount);
                Assert.Equal(6020m, invoice.BaseBalanceAmount);
            });
        Assert.Equal(120m, result.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            6020m,
            result.Value.Summary.BaseClosingBalanceAmount);
    }

    [Fact]
    public async Task DriverStatementIncludesGeneralLinkedAndTripCostRows()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(1);
        await AddVoucherAsync(vouchers,
            CreateDriverVoucher(
                "CV-DRIVER-GENERAL",
                new DateOnly(2026, 7, 18),
                CashDirection.Payment,
                tripId: null,
                amount: 100m));
        await AddVoucherAsync(vouchers,
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

        var generalVoucher = Assert.Single(
            overall.Value.Items,
            item => item.Description == "CV-DRIVER-GENERAL");
        Assert.Null(generalVoucher.BusinessPartnerId);
        Assert.Null(generalVoucher.BusinessPartnerName);
        Assert.All(
            tripSpecific.Value.Items,
            item =>
            {
                Assert.Equal(1, item.BusinessPartnerId);
                Assert.Equal(
                    "Customer One",
                    item.BusinessPartnerName);
            });
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
        var created = await AddVoucherAsync(vouchers,
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

    private static VoucherTestRequest CreateGeneralVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int movementTypeId,
        decimal amount) =>
        new(
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

    private static VoucherTestRequest CreatePartnerVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int movementTypeId,
        decimal amount) =>
        new(
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

    private static VoucherTestRequest CreateDriverVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int? tripId,
        decimal amount) =>
        new(
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

    private static async Task<Result<CashVoucherResponse>> AddVoucherAsync(
        ICashVoucherService service,
        VoucherTestRequest request)
    {
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                request.VoucherDate,
                request.Direction,
                request.CashboxId,
                request.Amount,
                request.Description));
        if (draft.IsFailure)
        {
            return draft;
        }

        return await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                request.VoucherDate,
                request.Direction,
                request.CashboxId,
                request.CashMovementTypeId,
                request.PartyType,
                request.BusinessPartnerId,
                request.DriverId,
                request.DriverTripId,
                request.ExternalPartyName,
                request.Amount,
                request.ReferenceNumber,
                request.Description,
                request.Notes,
                draft.Value.RowVersion));
    }

    private sealed record VoucherTestRequest(
        DateOnly VoucherDate,
        CashDirection Direction,
        int CashboxId,
        int CashMovementTypeId,
        CashPartyType PartyType,
        int? BusinessPartnerId,
        int? DriverId,
        int? DriverTripId,
        string? ExternalPartyName,
        decimal Amount,
        string? ReferenceNumber,
        string? Description,
        string? Notes);
}
