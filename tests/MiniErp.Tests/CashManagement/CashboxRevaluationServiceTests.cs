using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashboxRevaluations;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxRevaluationServiceTests
{
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
