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
    public async Task Revaluation_RevaluesSupplierCreditBalanceAndPostsLoss()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive, IsDeleted)
            VALUES
                (110, 1, '2100', 'Supplier Control', 50, 2, 2, 1, 1, 0);

            INSERT INTO AccountMappings (
                CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 10, NULL, 110, 'test', '2026-01-01', 'test', 0);
            """);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3301,
            entryNumber: "JE-USD-PAYABLE",
            entryDate: new DateOnly(2026, 1, 10),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 110,
                    PartyType: JournalPartyType.Supplier,
                    PartyId: 5,
                    Debit: 0m,
                    Credit: 48_000m,
                    Currency: CurrencyCode.USD,
                    ExchangeRate: 48m,
                    TransactionDebit: 0m,
                    TransactionCredit: 1_000m)
            ]);

        var result = await database.CreateMonetaryAccountRevaluationService(1)
            .CreateAsync(new MonetaryAccountRevaluationRequest(
                AccountId: 110,
                Currency: CurrencyCode.USD,
                PartyType: JournalPartyType.Supplier,
                PartyId: 5,
                RevaluationDate: new DateOnly(2026, 1, 31),
                ClosingRate: 50m));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.Equal(-1_000m, result.Value.ForeignAmount);
        Assert.Equal(-48_000m, result.Value.CarryingBaseAmount);
        Assert.Equal(-50_000m, result.Value.TargetBaseAmount);
        Assert.Equal(-2_000m, result.Value.DeltaBaseAmount);
        Assert.NotNull(result.Value.JournalEntryId);
        var journalLines = await database.Context.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.JournalEntryId == result.Value.JournalEntryId)
            .ToListAsync();
        Assert.Contains(journalLines, line =>
            line.AccountId == 110 &&
            line.PartyType == JournalPartyType.Supplier &&
            line.PartyId == 5 &&
            line.Credit == 2_000m);
        Assert.Contains(journalLines, line =>
            line.AccountId == 1 &&
            line.PartyType == null &&
            line.Debit == 2_000m);
    }

    [Fact]
    public async Task Revaluation_IncludesPriorYearAdjustmentCarriedIntoOpeningBalance()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await AddHistoricalFiscalYearAsync(database);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3401,
            entryNumber: "JE-2025-EUR",
            entryDate: new DateOnly(2025, 6, 1),
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
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3402,
            entryNumber: "JE-2025-REVALUATION",
            entryDate: new DateOnly(2025, 12, 31),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.MonetaryAccountRevaluation,
            sourceId: 90,
            sourceNumber: "MAR-90",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 500m,
                    TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
                    PartyType: null,
                    PartyId: null,
                    Debit: 0m,
                    Credit: 500m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 0m,
                    TransactionCredit: 500m)
            ],
            fiscalYearId: 3);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO MonetaryAccountRevaluations (
                Id, CompanyId, FiscalYearId, AccountId, Currency, PartyType,
                PartyId, RevaluationDate, ClosingRate, ForeignAmount,
                CarryingBaseAmount, TargetBaseAmount, DeltaBaseAmount,
                JournalEntryId, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (90, 1, 3, 100, 3, 5, 6, '2025-12-31', 55, 100,
                 5000, 5500, 500, 3402, 'test', '2025-12-31', 'test', 0);
            """);
        // Year-end carry-forward groups by currency: the EUR balance and the
        // base-currency revaluation adjustment arrive as separate lines.
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3403,
            entryNumber: "JE-OPENING-2026",
            entryDate: new DateOnly(2026, 1, 1),
            entryType: JournalEntryType.Opening,
            sourceType: JournalEntrySourceType.FiscalYearClosing,
            sourceId: 3,
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
                    TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 500m,
                    TransactionCredit: 0m)
            ]);

        var result = await database.CreateMonetaryAccountRevaluationService(1)
            .CreateAsync(new MonetaryAccountRevaluationRequest(
                AccountId: 100,
                Currency: CurrencyCode.EUR,
                PartyType: JournalPartyType.Cashbox,
                PartyId: 6,
                RevaluationDate: new DateOnly(2026, 1, 31),
                ClosingRate: 55m));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.Equal(100m, result.Value.ForeignAmount);
        Assert.Equal(5_500m, result.Value.CarryingBaseAmount);
        Assert.Equal(0m, result.Value.DeltaBaseAmount);
        Assert.Null(result.Value.JournalEntryId);
    }

    [Fact]
    public async Task Revaluation_IncludesEarlierCashboxRevaluationOfTheSameCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3501,
            entryNumber: "JE-EUR-RECEIPT",
            entryDate: new DateOnly(2026, 1, 10),
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
            ]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3502,
            entryNumber: "JE-CASHBOX-REVALUATION",
            entryDate: new DateOnly(2026, 1, 31),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.CashboxRevaluation,
            sourceId: 77,
            sourceNumber: "CBR-77",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 100,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 500m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 500m,
                    TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
                    PartyType: null,
                    PartyId: null,
                    Debit: 0m,
                    Credit: 500m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 0m,
                    TransactionCredit: 500m)
            ]);

        var result = await database.CreateMonetaryAccountRevaluationService(1)
            .CreateAsync(new MonetaryAccountRevaluationRequest(
                AccountId: 100,
                Currency: CurrencyCode.EUR,
                PartyType: JournalPartyType.Cashbox,
                PartyId: 6,
                RevaluationDate: new DateOnly(2026, 2, 28),
                ClosingRate: 55m));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.Equal(5_500m, result.Value.CarryingBaseAmount);
        Assert.Equal(0m, result.Value.DeltaBaseAmount);
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
