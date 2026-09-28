using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.MonetaryAccountRevaluations;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class MonetaryAccountRevaluationServiceTests
{
    [Fact]
    public async Task Options_DefaultToCurrentFiscalYearAndSupportHistoricalSelection()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await AddHistoricalFiscalYearAsync(database);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3001,
            entryNumber: "JE-CURRENT-MONETARY",
            entryDate: new DateOnly(2026, 1, 1),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 5_500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EUR,
                    ExchangeRate: 55m,
                    TransactionDebit: 100m,
                    TransactionCredit: 0m)
            ]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3002,
            entryNumber: "JE-HISTORICAL-MONETARY",
            entryDate: new DateOnly(2025, 12, 31),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 2,
                    PartyType: null,
                    PartyId: null,
                    Debit: 2_500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EUR,
                    ExchangeRate: 50m,
                    TransactionDebit: 50m,
                    TransactionCredit: 0m)
            ],
            fiscalYearId: 3);

        var service = database.CreateMonetaryAccountRevaluationService(1);

        var current = await service.GetOptionsAsync();
        var historical = await service.GetOptionsAsync(fiscalYearId: 3);

        Assert.True(current.IsSuccess);
        Assert.Contains(current.Value, row =>
            row.FiscalYearId == 1 &&
            row.FiscalYearName == "2026" &&
            row.AccountId == 100 &&
            row.Currency == CurrencyCode.EUR);
        Assert.DoesNotContain(current.Value, row => row.AccountId == 2);
        Assert.True(historical.IsSuccess);
        var historicalRow = Assert.Single(historical.Value);
        Assert.Equal(3, historicalRow.FiscalYearId);
        Assert.Equal("2025", historicalRow.FiscalYearName);
        Assert.Equal(2, historicalRow.AccountId);
    }

    [Fact]
    public async Task Revaluation_IsolatesFiscalYearAndCurrencyAndUsesOnlyPriorTargetDeltas()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await AddHistoricalFiscalYearAsync(database);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3101,
            entryNumber: "JE-CURRENT-MIXED-CURRENCIES",
            entryDate: new DateOnly(2026, 1, 1),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 5_500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EUR,
                    ExchangeRate: 55m,
                    TransactionDebit: 100m,
                    TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 10_000m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 10_000m,
                    TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 5_400m,
                    Credit: 0m,
                    Currency: CurrencyCode.USD,
                    ExchangeRate: 54m,
                    TransactionDebit: 100m,
                    TransactionCredit: 0m)
            ]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3102,
            entryNumber: "JE-HISTORICAL-EUR",
            entryDate: new DateOnly(2025, 12, 31),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 5_000m,
                    Credit: 0m,
                    Currency: CurrencyCode.EUR,
                    ExchangeRate: 50m,
                    TransactionDebit: 100m,
                    TransactionCredit: 0m)
            ],
            fiscalYearId: 3);

        var service = database.CreateMonetaryAccountRevaluationService(1);
        var first = await service.CreateAsync(new MonetaryAccountRevaluationRequest(
            AccountId: 100,
            Currency: CurrencyCode.EUR,
            PartyType: JournalPartyType.Cashbox,
            PartyId: 6,
            RevaluationDate: new DateOnly(2026, 1, 31),
            ClosingRate: 60m));

        Assert.True(first.IsSuccess, string.Join("; ", first.Errors.Select(error => error.Code)));
        Assert.Equal(1, first.Value.FiscalYearId);
        Assert.Equal("2026", first.Value.FiscalYearName);
        Assert.Equal(100m, first.Value.ForeignAmount);
        Assert.Equal(5_500m, first.Value.CarryingBaseAmount);
        Assert.Equal(500m, first.Value.DeltaBaseAmount);

        var second = await service.CreateAsync(new MonetaryAccountRevaluationRequest(
            AccountId: 100,
            Currency: CurrencyCode.EUR,
            PartyType: JournalPartyType.Cashbox,
            PartyId: 6,
            RevaluationDate: new DateOnly(2026, 2, 28),
            ClosingRate: 58m));

        Assert.True(second.IsSuccess, string.Join("; ", second.Errors.Select(error => error.Code)));
        Assert.Equal(100m, second.Value.ForeignAmount);
        Assert.Equal(6_000m, second.Value.CarryingBaseAmount);
        Assert.Equal(-200m, second.Value.DeltaBaseAmount);
        Assert.NotNull(second.Value.JournalEntryId);
    }

    [Fact]
    public async Task Get_DefaultsToCurrentFiscalYearAndReturnsSelectedHistory()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await AddHistoricalFiscalYearAsync(database);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO MonetaryAccountRevaluations (
                Id, CompanyId, FiscalYearId, AccountId, Currency, PartyType,
                PartyId, RevaluationDate, ClosingRate, ForeignAmount,
                CarryingBaseAmount, TargetBaseAmount, DeltaBaseAmount,
                JournalEntryId, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (81, 1, 3, 100, 3, 5, 6, '2025-12-31', 50, 100,
                 4800, 5000, 200, NULL, 'test', '2025-12-31', 'test', 0),
                (82, 1, 1, 100, 3, 5, 6, '2026-01-31', 60, 100,
                 5500, 6000, 500, NULL, 'test', '2026-01-31', 'test', 0);
            """);

        var service = database.CreateMonetaryAccountRevaluationService(1);
        var current = await service.GetAsync();
        var historical = await service.GetAsync(fiscalYearId: 3);

        Assert.True(current.IsSuccess);
        var currentRow = Assert.Single(current.Value);
        Assert.Equal(82, currentRow.Id);
        Assert.Equal(1, currentRow.FiscalYearId);
        Assert.Equal("2026", currentRow.FiscalYearName);
        Assert.True(historical.IsSuccess);
        var historicalRow = Assert.Single(historical.Value);
        Assert.Equal(81, historicalRow.Id);
        Assert.Equal(3, historicalRow.FiscalYearId);
        Assert.Equal("2025", historicalRow.FiscalYearName);
    }

    private static Task AddHistoricalFiscalYearAsync(
        CashManagementTestDatabase database) =>
        database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (3, 1, '2025', '2025-01-01', '2025-12-31', 2, 0,
                 'test', '2025-01-01', 'test', 0);
            """);
}
