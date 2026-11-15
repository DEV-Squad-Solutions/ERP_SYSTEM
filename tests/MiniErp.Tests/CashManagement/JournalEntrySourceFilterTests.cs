using System.Text.Json;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.JournalEntries;

namespace MiniErp.Tests.CashManagement;

public sealed class JournalEntrySourceFilterTests
{
    [Fact]
    public async Task ExactSourceLookupKeepsCompanyFiscalYearAndCashboxVisibilityBoundaries()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await SeedAsync(database, id: 2001, sourceType: JournalEntrySourceType.CashVoucher, sourceId: 700);
        await SeedAsync(database, id: 2002, sourceType: JournalEntrySourceType.Invoice, sourceId: 700);
        await SeedAsync(database, id: 2003, sourceType: JournalEntrySourceType.CashVoucher, sourceId: 701);
        await SeedAsync(database, id: 2004, sourceType: JournalEntrySourceType.CashVoucher, sourceId: 700,
            companyId: 2);
        await SeedAsync(database, id: 2005, sourceType: JournalEntrySourceType.CashVoucher, sourceId: 700,
            fiscalYearId: 1);
        var company = new CompanyContext(1);
        var service = new JournalEntryService(database.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(database.Context, company));
        var filter = JsonSerializer.Deserialize<JournalEntryFilterRequest>(
            """{"fiscalYearId":100,"sourceType":2,"sourceId":700,"entryType":4,"status":1}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var result = await service.GetAllAsync(new PaginationRequest(), filter);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(1, result.Value.TotalCount);
        var entry = Assert.Single(result.Value.Items);
        Assert.Equal(2001, entry.Id);
        Assert.Equal(1, entry.CompanyId);
        Assert.Equal(100, entry.FiscalYearId);
        Assert.Equal(JournalEntrySourceType.CashVoucher, entry.SourceType);
        Assert.Equal(700, entry.SourceId);
        Assert.Equal(JournalEntryType.Automatic, entry.EntryType);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(2, entry.Lines.Count);
        var cashLine = Assert.Single(entry.Lines, line => line.PartyType == JournalPartyType.Cashbox);
        Assert.Equal(20m, cashLine.Debit);
        Assert.Equal(0m, cashLine.Credit);
        Assert.Equal(1, cashLine.PartyId);

        var historical = await service.GetAllAsync(new PaginationRequest(), filter with { FiscalYearId = 1 });
        Assert.True(historical.IsSuccess, historical.Error.Description);
        Assert.Equal(2005, Assert.Single(historical.Value.Items).Id);

        var restricted = new JournalEntryService(database.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(database.Context, company),
            new RestrictedCashboxAccess(view: [2], execute: []));
        var hidden = await restricted.GetAllAsync(new PaginationRequest(), filter);
        Assert.True(hidden.IsSuccess, hidden.Error.Description);
        Assert.Empty(hidden.Value.Items);
        Assert.Equal(0, hidden.Value.TotalCount);
    }

    [Theory]
    [InlineData("{\"sourceType\":2}")]
    [InlineData("{\"sourceId\":700}")]
    [InlineData("{\"sourceType\":999,\"sourceId\":700}")]
    [InlineData("{\"sourceType\":2,\"sourceId\":0}")]
    public void SourceFilterRequiresValidTypeAndPositiveIdTogether(string json)
    {
        var filter = JsonSerializer.Deserialize<JournalEntryFilterRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(new JournalEntryFilterRequestValidator().Validate(filter).IsValid);
    }

    private static Task SeedAsync(CashManagementTestDatabase database, int id,
        JournalEntrySourceType sourceType, int sourceId, int companyId = 1, int fiscalYearId = 100) =>
        database.SeedPostedJournalEntryAsync(
            journalEntryId: id, entryNumber: $"JE-SOURCE-{id}",
            entryDate: new DateOnly(fiscalYearId == 1 ? 2025 : 2026, 7, 1),
            entryType: JournalEntryType.Automatic, sourceType: sourceType,
            sourceId: sourceId, sourceNumber: "SRC", companyId: companyId,
            fiscalYearId: fiscalYearId, lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(AccountId: 100,
                    PartyType: JournalPartyType.Cashbox, PartyId: 1,
                    Debit: 20m, Credit: 0m, Currency: CurrencyCode.EGP, ExchangeRate: 1m,
                    TransactionDebit: 20m, TransactionCredit: 0m),
                new CashManagementTestDatabase.JournalEntryLineSeed(AccountId: 1,
                    PartyType: null, PartyId: null, Debit: 0m, Credit: 20m,
                    Currency: CurrencyCode.EGP, ExchangeRate: 1m,
                    TransactionDebit: 0m, TransactionCredit: 20m)
            ]);

    private sealed record CompanyContext(int CompanyId) : ICurrentCompanyContext;
}
