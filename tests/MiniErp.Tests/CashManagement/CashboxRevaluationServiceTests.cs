using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashboxRevaluations;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxRevaluationServiceTests
{
    [Fact]
    public async Task Get_DefaultsToCurrentFiscalYearAndSupportsHistoricalSelection()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (3, 1, '2025', '2025-01-01', '2025-12-31', 2, 0,
                 'test', '2025-01-01', 'test', 0);

            INSERT INTO CashboxRevaluations (
                Id, CompanyId, FiscalYearId, CashboxId, RevaluationDate,
                ClosingRate, ForeignAmount, CarryingBaseAmount,
                TargetBaseAmount, DeltaBaseAmount, JournalEntryId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (91, 1, 3, 6, '2025-12-31', 50, 100, 4800, 5000, 200,
                 NULL, 'test', '2025-12-31', 'test', 0),
                (92, 1, 1, 6, '2026-01-31', 60, 100, 5500, 6000, 500,
                 NULL, 'test', '2026-01-31', 'test', 0);
            """);

        var service = database.CreateCashboxRevaluationService(1);

        var current = await service.GetAsync();
        var historical = await service.GetAsync(fiscalYearId: 3);

        Assert.True(current.IsSuccess);
        var currentRow = Assert.Single(current.Value);
        Assert.Equal(1, currentRow.FiscalYearId);
        Assert.Equal("2026", currentRow.FiscalYearName);
        Assert.Equal(92, currentRow.Id);
        Assert.True(historical.IsSuccess);
        var historicalRow = Assert.Single(historical.Value);
        Assert.Equal(3, historicalRow.FiscalYearId);
        Assert.Equal("2025", historicalRow.FiscalYearName);
        Assert.Equal(91, historicalRow.Id);
    }

    [Fact]
    public async Task Revaluation_UsesOnlyTheDateFiscalYearLedgerAndBackdatedHistory()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (3, 1, '2025', '2025-01-01', '2025-12-31', 2, 0,
                 'test', '2025-01-01', 'test', 0),
                (4, 1, '2027', '2027-01-01', '2027-12-31', 1, 0,
                 'test', '2027-01-01', 'test', 0);

            UPDATE JournalEntryLines
            SET Debit = 5500, Currency = 3, ExchangeRate = 55,
                TransactionDebit = 100
            WHERE Id = 1006;

            INSERT INTO CashboxRevaluations (
                CompanyId, FiscalYearId, CashboxId, RevaluationDate,
                ClosingRate, ForeignAmount, CarryingBaseAmount,
                TargetBaseAmount, DeltaBaseAmount, JournalEntryId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 4, 6, '2027-06-30', 70, 100, 6500, 7000, 500,
                 NULL, 'test', '2027-06-30', 'test', 0);
            """);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 2001,
            entryNumber: "JE-OLD-EUR",
            entryDate: new DateOnly(2025, 12, 31),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
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

        var result = await database.CreateCashboxRevaluationService(1)
            .CreateAsync(new CashboxRevaluationRequest(
                CashboxId: 6,
                RevaluationDate: new DateOnly(2026, 1, 31),
                ClosingRate: 60m));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.Equal(1, result.Value.FiscalYearId);
        Assert.Equal("2026", result.Value.FiscalYearName);
        Assert.Equal(100m, result.Value.ForeignAmount);
        Assert.Equal(5_500m, result.Value.CarryingBaseAmount);
        Assert.Equal(500m, result.Value.DeltaBaseAmount);
    }

    [Fact]
    public async Task Revaluation_PostsGainThenIncrementalLossWithoutChangingForeignQuantity()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE JournalEntryLines SET Debit = 55000, Currency = 3, " +
            "ExchangeRate = 55, TransactionDebit = 1000 WHERE Id = 1006");

        var service = database.CreateCashboxRevaluationService(1);
        var first = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 1, 31),
            ClosingRate: 60m));

        Assert.True(first.IsSuccess, string.Join("; ", first.Errors.Select(error => error.Code)));
        Assert.Equal(1_000m, first.Value.ForeignAmount);
        Assert.Equal(60_000m, first.Value.TargetBaseAmount);
        Assert.Equal(5_000m, first.Value.DeltaBaseAmount);
        Assert.NotNull(first.Value.JournalEntryId);

        var second = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 2, 28),
            ClosingRate: 58m));

        Assert.True(second.IsSuccess, string.Join("; ", second.Errors.Select(error => error.Code)));
        Assert.Equal(1_000m, second.Value.ForeignAmount);
        Assert.Equal(58_000m, second.Value.TargetBaseAmount);
        Assert.Equal(-2_000m, second.Value.DeltaBaseAmount);

        var duplicate = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 2, 28),
            ClosingRate: 59m));
        Assert.True(duplicate.IsFailure);
        Assert.Contains(duplicate.Errors, error => error.Code == "CashboxRevaluations.Duplicate");

        var backdated = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 1, 15),
            ClosingRate: 57m));
        Assert.True(backdated.IsFailure);
        Assert.Contains(backdated.Errors, error => error.Code == "CashboxRevaluations.Backdated");

        var statement = await database.CreateStatementService(1).GetCashboxStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashboxStatementFilterRequest(CashboxId: 6));
        Assert.True(statement.IsSuccess, string.Join("; ", statement.Errors.Select(error => error.Code)));
        var revaluationRows = statement.Value.Items
            .Where(item => item.SourceType == JournalEntrySourceType.CashboxRevaluation)
            .ToArray();
        Assert.Equal(2, revaluationRows.Length);
        Assert.All(revaluationRows, item =>
        {
            Assert.Equal(CurrencyCode.EUR, item.Currency);
            Assert.Equal(0m, item.ReceiptAmount);
            Assert.Equal(0m, item.PaymentAmount);
        });
        Assert.Contains(revaluationRows, item => item.ExchangeRate == 60m && item.BaseReceiptAmount == 5_000m);
        Assert.Contains(revaluationRows, item => item.ExchangeRate == 58m && item.BasePaymentAmount == 2_000m);
        Assert.Equal(1_000m, statement.Value.Summary.ClosingBalance);
        Assert.Equal(58_000m, statement.Value.Summary.BaseClosingBalance);

        var journalLines = await database.Context.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.JournalEntry.SourceType == JournalEntrySourceType.CashboxRevaluation)
            .ToListAsync();
        Assert.Contains(journalLines, line => line.Debit == 5_000m && line.PartyType == JournalPartyType.Cashbox && line.PartyId == 6);
        Assert.Contains(journalLines, line => line.Credit == 5_000m && line.PartyType == null);
        Assert.Contains(journalLines, line => line.Credit == 2_000m && line.PartyType == JournalPartyType.Cashbox && line.PartyId == 6);
        Assert.Contains(journalLines, line => line.Debit == 2_000m && line.PartyType == null);
        Assert.Equal(2, await database.Context.CashboxRevaluations.CountAsync());
    }

    [Fact]
    public async Task Revaluation_CorrectsNegativeCarryingAmountForPositiveCash()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        // Receive 100 EUR at 50 (5,000), then pay 90 EUR at 60 (5,400):
        // 10 EUR remain while the carrying base amount is -400.
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE JournalEntryLines SET Debit = 5000, Currency = 3, " +
            "ExchangeRate = 50, TransactionDebit = 100 WHERE Id = 1006");
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 2101,
            entryNumber: "JE-EUR-PAYMENT",
            entryDate: new DateOnly(2026, 1, 20),
            entryType: JournalEntryType.Automatic,
            sourceType: null,
            sourceId: null,
            sourceNumber: null,
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
                    PartyType: JournalPartyType.Cashbox,
                    PartyId: 6,
                    Debit: 0m,
                    Credit: 5_400m,
                    Currency: CurrencyCode.EUR,
                    ExchangeRate: 60m,
                    TransactionDebit: 0m,
                    TransactionCredit: 90m)
            ]);

        var result = await database.CreateCashboxRevaluationService(1)
            .CreateAsync(new CashboxRevaluationRequest(
                CashboxId: 6,
                RevaluationDate: new DateOnly(2026, 1, 31),
                ClosingRate: 60m));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Code)));
        Assert.Equal(10m, result.Value.ForeignAmount);
        Assert.Equal(-400m, result.Value.CarryingBaseAmount);
        Assert.Equal(600m, result.Value.TargetBaseAmount);
        Assert.Equal(1_000m, result.Value.DeltaBaseAmount);
        Assert.NotNull(result.Value.JournalEntryId);
    }

    [Fact]
    public async Task Revaluation_RejectsClosedFiscalYearAndBaseCurrencyCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxRevaluationService(1);

        var baseCurrency = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 1,
            RevaluationDate: new DateOnly(2026, 1, 31),
            ClosingRate: 60m));
        Assert.True(baseCurrency.IsFailure);
        Assert.Contains(baseCurrency.Errors, error => error.Code == "CashboxRevaluations.BaseCurrencyCashbox");

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE FiscalYears SET Status = 2 WHERE Id = 1 AND CompanyId = 1");
        var closed = await service.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 3, 31),
            ClosingRate: 60m));
        Assert.True(closed.IsFailure);
        Assert.Contains(closed.Errors, error => error.Code == "CashboxRevaluations.FiscalYearClosed");

        var otherCompany = database.CreateCashboxRevaluationService(2);
        var isolated = await otherCompany.CreateAsync(new CashboxRevaluationRequest(
            CashboxId: 6,
            RevaluationDate: new DateOnly(2026, 4, 30),
            ClosingRate: 60m));
        Assert.True(isolated.IsFailure);
        Assert.Contains(isolated.Errors, error => error.Code == "CashboxRevaluations.CashboxNotFound");
    }
}
