using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.MonetaryAccountRevaluations;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class ForeignCurrencyStatementRevaluationTests
{
    static ForeignCurrencyStatementRevaluationTests()
    {
        MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Theory]
    [InlineData(JournalPartyType.Customer)]
    [InlineData(JournalPartyType.Supplier)]
    public async Task PartnerRevaluationPreservesForeignUnitsInMovementsAndOpeningBalances(
        JournalPartyType partyType)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var isSupplier = partyType == JournalPartyType.Supplier;
        await SeedForeignBalanceAsync(database, partyType, isSupplier);
        var revaluation = await RevalueAsync(database, partyType);
        var statements = database.CreateStatementService(1);

        var current = await statements.GetPartnerStatementAsync(
            Page(), new PartnerStatementFilterRequest(BusinessPartnerId: 5));

        Assert.True(current.IsSuccess);
        Assert.Equal(CurrencyCode.USD, current.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, current.Value.BaseCurrency);
        Assert.Equal(2, current.Value.TotalCount);
        Assert.Equal(100m, current.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(5_100m, current.Value.Summary.BaseClosingBalanceAmount);
        Assert.Equal(isSupplier ? "له" : "عليه", current.Value.Summary.ClosingBalanceDescription);
        var adjustment = Assert.Single(current.Value.Items,
            row => row.JournalEntryId == revaluation.JournalEntryId);
        Assert.NotNull(adjustment.JournalEntryLineId);
        Assert.Equal(PartnerStatementSourceType.JournalEntry, adjustment.SourceType);
        Assert.Equal(0m, adjustment.DebitAmount);
        Assert.Equal(0m, adjustment.CreditAmount);
        Assert.Equal(51m, adjustment.ExchangeRate);
        Assert.Equal(isSupplier ? 0m : 100m, adjustment.BaseDebitAmount);
        Assert.Equal(isSupplier ? 100m : 0m, adjustment.BaseCreditAmount);
        Assert.Equal(100m, adjustment.BalanceAmount);
        Assert.Equal(5_100m, adjustment.BaseBalanceAmount);

        // Pagination must keep both running balances when the first native
        // movement is on a preceding page.
        var secondPage = await statements.GetPartnerStatementAsync(
            Page(pageNumber: 2, pageSize: 1),
            new PartnerStatementFilterRequest(BusinessPartnerId: 5));
        Assert.True(secondPage.IsSuccess);
        var pagedAdjustment = Assert.Single(secondPage.Value.Items);
        Assert.Equal(adjustment.JournalEntryLineId, pagedAdjustment.JournalEntryLineId);
        Assert.Equal(100m, pagedAdjustment.BalanceAmount);
        Assert.Equal(5_100m, pagedAdjustment.BaseBalanceAmount);

        var laterPeriod = await statements.GetPartnerStatementAsync(
            Page(), new PartnerStatementFilterRequest(
                BusinessPartnerId: 5, FromDate: new DateOnly(2026, 2, 1)));
        Assert.True(laterPeriod.IsSuccess);
        Assert.Empty(laterPeriod.Value.Items);
        Assert.Equal(100m, laterPeriod.Value.Summary.OpeningBalanceAmount);
        Assert.Equal(5_100m, laterPeriod.Value.Summary.BaseOpeningBalanceAmount);

        await SeedCarriedOpeningAsync(database, partyType);
        var nextYear = await statements.GetPartnerStatementAsync(
            Page(), new PartnerStatementFilterRequest(BusinessPartnerId: 5, FiscalYearId: 3));
        Assert.True(nextYear.IsSuccess);
        Assert.Empty(nextYear.Value.Items);
        Assert.Equal(0, nextYear.Value.TotalCount);
        Assert.Equal(100m, nextYear.Value.Summary.OpeningBalanceAmount);
        Assert.Equal(5_100m, nextYear.Value.Summary.BaseOpeningBalanceAmount);
        Assert.Equal(100m, nextYear.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(5_100m, nextYear.Value.Summary.BaseClosingBalanceAmount);
        Assert.Equal(isSupplier ? "له" : "عليه", nextYear.Value.Summary.ClosingBalanceDescription);
    }

    [Fact]
    public async Task CashboxRevaluationPreservesForeignUnitsInMovementsAndOpeningBalances()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedForeignBalanceAsync(database, JournalPartyType.Cashbox, isCredit: false);
        var revaluation = await RevalueAsync(database, JournalPartyType.Cashbox);
        var statements = database.CreateStatementService(1);

        var current = await statements.GetCashboxStatementAsync(
            Page(), new CashboxStatementFilterRequest(CashboxId: 5));
        Assert.True(current.IsSuccess);
        Assert.Equal(2, current.Value.TotalCount);
        Assert.Equal(100m, current.Value.Summary.OpeningBalance);
        Assert.Equal(5_000m, current.Value.Summary.BaseOpeningBalance);
        Assert.Equal(0m, current.Value.Summary.TotalReceipts);
        Assert.Equal(0m, current.Value.Summary.TotalPayments);
        Assert.Equal(100m, current.Value.Summary.BaseTotalReceipts);
        Assert.Equal(0m, current.Value.Summary.BaseTotalPayments);
        Assert.Equal(100m, current.Value.Summary.ClosingBalance);
        Assert.Equal(5_100m, current.Value.Summary.BaseClosingBalance);
        var adjustment = Assert.Single(current.Value.Items,
            row => row.JournalEntryId == revaluation.JournalEntryId);
        Assert.NotNull(adjustment.JournalEntryLineId);
        Assert.Equal(JournalEntrySourceType.MonetaryAccountRevaluation, adjustment.SourceType);
        Assert.Equal("إعادة تقييم عملة", adjustment.MovementName);
        Assert.Equal(CurrencyCode.USD, adjustment.Currency);
        Assert.Equal(CurrencyCode.EGP, adjustment.BaseCurrency);
        Assert.False(adjustment.IsBaseCurrency);
        Assert.Equal(51m, adjustment.ExchangeRate);
        Assert.Equal(0m, adjustment.ReceiptAmount);
        Assert.Equal(0m, adjustment.PaymentAmount);
        Assert.Equal(100m, adjustment.BaseReceiptAmount);
        Assert.Equal(0m, adjustment.BasePaymentAmount);
        Assert.Equal(100m, adjustment.Balance);
        Assert.Equal(5_100m, adjustment.BaseBalance);

        var secondPage = await statements.GetCashboxStatementAsync(
            Page(pageNumber: 2, pageSize: 1),
            new CashboxStatementFilterRequest(CashboxId: 5));
        Assert.True(secondPage.IsSuccess);
        var pagedAdjustment = Assert.Single(secondPage.Value.Items);
        Assert.Equal(adjustment.JournalEntryLineId, pagedAdjustment.JournalEntryLineId);
        Assert.Equal(100m, pagedAdjustment.Balance);
        Assert.Equal(5_100m, pagedAdjustment.BaseBalance);

        var laterPeriod = await statements.GetCashboxStatementAsync(
            Page(), new CashboxStatementFilterRequest(
                CashboxId: 5, FromDate: new DateOnly(2026, 2, 1)));
        Assert.True(laterPeriod.IsSuccess);
        var periodOpening = Assert.Single(laterPeriod.Value.Items);
        Assert.Equal(100m, periodOpening.Balance);
        Assert.Equal(5_100m, periodOpening.BaseBalance);
        Assert.Equal(100m, laterPeriod.Value.Summary.OpeningBalance);
        Assert.Equal(5_100m, laterPeriod.Value.Summary.BaseOpeningBalance);

        await SeedCarriedOpeningAsync(database, JournalPartyType.Cashbox);
        var nextYear = await statements.GetCashboxStatementAsync(
            Page(), new CashboxStatementFilterRequest(CashboxId: 5, FiscalYearId: 3));
        Assert.True(nextYear.IsSuccess);
        Assert.Equal(1, nextYear.Value.TotalCount);
        var yearOpening = Assert.Single(nextYear.Value.Items);
        Assert.Equal(100m, yearOpening.Balance);
        Assert.Equal(5_100m, yearOpening.BaseBalance);
        Assert.Equal(100m, nextYear.Value.Summary.OpeningBalance);
        Assert.Equal(5_100m, nextYear.Value.Summary.BaseOpeningBalance);
        Assert.Equal(100m, nextYear.Value.Summary.ClosingBalance);
        Assert.Equal(5_100m, nextYear.Value.Summary.BaseClosingBalance);

        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears SET IsCurrent = 0 WHERE Id = 1;
            UPDATE FiscalYears SET IsCurrent = 1 WHERE Id = 3;
            """);
        var cashboxes = database.CreateCashboxService(1);
        var details = await cashboxes.GetByIdAsync(5);
        var list = await cashboxes.GetAllAsync(Page());
        var select = await cashboxes.GetSelectAsync();
        Assert.True(details.IsSuccess);
        Assert.Equal(CurrencyCode.USD, details.Value.Currency);
        Assert.Equal(100m, details.Value.CurrentBalance);
        Assert.True(list.IsSuccess);
        Assert.Equal(100m, Assert.Single(list.Value.Items, row => row.Id == 5).CurrentBalance);
        Assert.True(select.IsSuccess);
        Assert.Equal(100m, Assert.Single(select.Value, row => row.Id == 5).CurrentBalance);
    }

    private static async Task SeedForeignBalanceAsync(
        CashManagementTestDatabase database,
        JournalPartyType partyType,
        bool isCredit)
    {
        if (partyType == JournalPartyType.Cashbox)
        {
            await database.Context.Database.ExecuteSqlRawAsync(
                """
                UPDATE Cashboxes
                SET OpeningExchangeRate = 50, BaseOpeningBalance = 5000
                WHERE Id = 5;
                UPDATE JournalEntryLines
                SET AccountId = 100, Debit = 5000, ExchangeRate = 50
                WHERE JournalEntryId = 1005;
                """);
            return;
        }

        var mappingType = isCredit
            ? AccountingMappingType.SupplierControl
            : AccountingMappingType.CustomerControl;
        var accountType = isCredit ? AccountType.Liability : AccountType.Asset;
        var accountId = TargetAccountId(partyType);
        var accountCode = isCredit ? "2100" : "1200";
        var accountName = isCredit ? "Supplier Control" : "Customer Control";
        var normalBalance = isCredit ? 2 : 1;
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive, IsDeleted)
            VALUES (
                {accountId}, 1, {accountCode}, {accountName},
                50, {(int)accountType}, {normalBalance}, 1, 1, 0);
            INSERT INTO AccountMappings (
                CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                1, 1, {(int)mappingType}, NULL, {accountId},
                'test', '2026-01-01', 'test', 0);
            """);

        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3701,
            entryNumber: "JE-USD-PARTNER-BALANCE",
            entryDate: new DateOnly(2026, 1, 1),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: accountId,
                    PartyType: partyType,
                    PartyId: 5,
                    Debit: isCredit ? 0m : 5_000m,
                    Credit: isCredit ? 5_000m : 0m,
                    Currency: CurrencyCode.USD,
                    ExchangeRate: 50m,
                    TransactionDebit: isCredit ? 0m : 100m,
                    TransactionCredit: isCredit ? 100m : 0m)
            ]);
    }

    private static async Task<MonetaryAccountRevaluationResponse> RevalueAsync(
        CashManagementTestDatabase database,
        JournalPartyType partyType)
    {
        var result = await database.CreateMonetaryAccountRevaluationService(1)
            .CreateAsync(new MonetaryAccountRevaluationRequest(
                AccountId: TargetAccountId(partyType),
                Currency: CurrencyCode.USD,
                PartyType: partyType,
                PartyId: 5,
                RevaluationDate: new DateOnly(2026, 1, 31),
                ClosingRate: 51m));
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.NotNull(result.Value.JournalEntryId);
        Assert.Equal(100m, Math.Abs(result.Value.ForeignAmount));
        Assert.Equal(5_000m, Math.Abs(result.Value.CarryingBaseAmount));
        Assert.Equal(5_100m, Math.Abs(result.Value.TargetBaseAmount));
        Assert.Equal(100m, Math.Abs(result.Value.DeltaBaseAmount));
        return result.Value;
    }

    private static async Task SeedCarriedOpeningAsync(
        CashManagementTestDatabase database,
        JournalPartyType partyType)
    {
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent)
            VALUES (3, 1, '2027', '2027-01-01', '2027-12-31', 1, 0);
            """);

        // Fiscal-year closing groups target balances by currency. Seed that
        // persisted shape from the actual original/revaluation journal lines:
        // one USD line for 100 units/5000 EGP and one EGP delta line for 100.
        var sourceLines = await database.Context.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.CompanyId == 1 &&
                line.JournalEntry.FiscalYearId == 1 &&
                line.PartyType == partyType && line.PartyId == 5)
            .ToListAsync();
        var openingLines = sourceLines.GroupBy(line => line.Currency)
            .Select(group =>
            {
                var baseBalance = group.Sum(line => line.Debit - line.Credit);
                var nativeBalance = group.Sum(line => line.TransactionDebit - line.TransactionCredit);
                return new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: TargetAccountId(partyType),
                    PartyType: partyType,
                    PartyId: 5,
                    Debit: Math.Max(baseBalance, 0m),
                    Credit: Math.Max(-baseBalance, 0m),
                    Currency: group.Key,
                    ExchangeRate: Math.Abs(baseBalance / nativeBalance),
                    TransactionDebit: Math.Max(nativeBalance, 0m),
                    TransactionCredit: Math.Max(-nativeBalance, 0m));
            }).ToArray();
        Assert.Equal(2, openingLines.Length);
        var openingJournalEntryId = await database.Context.JournalEntries
            .IgnoreQueryFilters()
            .MaxAsync(entry => entry.Id) + 1;
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: openingJournalEntryId,
            entryNumber: "JE-CARRIED-USD-BALANCE",
            entryDate: new DateOnly(2027, 1, 1),
            entryType: JournalEntryType.Opening,
            sourceType: JournalEntrySourceType.FiscalYearClosing,
            sourceId: 1,
            sourceNumber: "2026",
            lines: openingLines,
            fiscalYearId: 3);
    }

    private static PaginationRequest Page(int pageNumber = 1, int pageSize = 20) =>
        new() { PageNumber = pageNumber, PageSize = pageSize };

    private static int TargetAccountId(JournalPartyType partyType) => partyType switch
    {
        JournalPartyType.Customer => 111,
        JournalPartyType.Supplier => 110,
        _ => 100
    };
}
