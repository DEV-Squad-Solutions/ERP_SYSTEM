using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Services.Statements;
using MiniErp.Infrastructure.Services.JournalEntries;
using MiniErp.Infrastructure.Services.FiscalYears;
using Microsoft.EntityFrameworkCore;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxReportAccessTests
{
    [Fact]
    public async Task HiddenCashboxStatement_IsForbiddenBeforeReadingFinancialRows()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = new FinancialStatementService(database.Context, new CompanyContext(1), new RestrictedAccess([1]));
        var result = await service.GetCashboxStatementAsync(new PaginationRequest(), new CashboxStatementFilterRequest(CashboxId: 2));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task RestrictedCashboxUser_CannotReadPartialFullCompanyFinancialReports()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = new FinancialStatementService(database.Context, new CompanyContext(1), new RestrictedAccess([1]));
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);
        var trial = await service.GetTrialBalanceAsync(new TrialBalanceFilterRequest(FromDate: from, ToDate: to));
        var financial = await service.GetFinancialStatementReportAsync(FinancialStatementType.IncomeStatement,
            new FinancialStatementReportRequest(FromDate: from, ToDate: to));
        var operational = await service.GetOperationalTrialBalanceAsync(new OperationalTrialBalanceFilterRequest(FromDate: from, ToDate: to));
        Assert.Equal(ErrorType.Forbidden, trial.Error.Type);
        Assert.Equal(ErrorType.Forbidden, financial.Error.Type);
        Assert.Equal(ErrorType.Forbidden, operational.Error.Type);
        Assert.Equal("CashboxPermissions.FullCompanyRequired", financial.Error.Code);
    }

    [Fact]
    public async Task JournalList_DoesNotShowAnyEntryWithHiddenCashboxLine()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 901, entryNumber: "JE-HIDDEN", entryDate: new DateOnly(2026, 1, 2),
            entryType: JournalEntryType.Manual, sourceType: null, sourceId: null, sourceNumber: null,
            lines: [new CashManagementTestDatabase.JournalEntryLineSeed(AccountId: 100, PartyType: JournalPartyType.Cashbox,
                PartyId: 2, Debit: 20m, Credit: 0m, Currency: CurrencyCode.EGP, ExchangeRate: 1m, TransactionDebit: 20m, TransactionCredit: 0m)]);
        var company = new CompanyContext(1);
        var service = new JournalEntryService(database.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(database.Context, company), new RestrictedAccess([1]));
        var list = await service.GetAllAsync(new PaginationRequest(), new JournalEntryFilterRequest(FiscalYearId: 1));
        var detail = await service.GetByIdAsync(901, fiscalYearId: 1);
        Assert.True(list.IsSuccess);
        Assert.DoesNotContain(list.Value.Items, item => item.Id == 901);
        Assert.Equal(ErrorType.Forbidden, detail.Error.Type);
    }

    [Fact]
    public async Task ViewOnlyUser_CannotDeleteExistingCashboxJournal()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 902, entryNumber: "JE-VIEW-ONLY", entryDate: new DateOnly(2026, 1, 2),
            entryType: JournalEntryType.Manual, sourceType: null, sourceId: null, sourceNumber: null,
            lines: [new CashManagementTestDatabase.JournalEntryLineSeed(AccountId: 100, PartyType: JournalPartyType.Cashbox,
                PartyId: 1, Debit: 20m, Credit: 0m, Currency: CurrencyCode.EGP, ExchangeRate: 1m, TransactionDebit: 20m, TransactionCredit: 0m)]);
        var rowVersion = await database.Context.JournalEntries.Where(entry => entry.Id == 902)
            .Select(entry => entry.RowVersion).SingleAsync();
        var company = new CompanyContext(1);
        var service = new JournalEntryService(database.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(database.Context, company), new RestrictedAccess([1]));
        var result = await service.DeleteAsync(902, rowVersion);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("CashboxPermissions.ExecuteDenied", result.Error.Code);
        Assert.True(await database.Context.JournalEntries.AnyAsync(entry => entry.Id == 902));
    }

    private sealed record CompanyContext(int CompanyId) : ICurrentCompanyContext;
    private sealed class RestrictedAccess(int[] visibleIds) : ICashboxAccessService
    {
        public Task<Result<CashboxAccessScope>> GetScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<CashboxAccessScope>.Success(new CashboxAccessScope(
                HasAllCashboxes: false, ViewCashboxIds: visibleIds, ExecuteCashboxIds: [])));
        public Task<Result> EnsureViewAsync(int cashboxId, CancellationToken cancellationToken = default)
            => Task.FromResult(visibleIds.Contains(cashboxId) ? Result.Success()
                : Result.Failure(Error.Forbidden("CashboxPermissions.ViewDenied", "Denied")));
        public Task<Result> EnsureExecuteAsync(int cashboxId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure(Error.Forbidden("CashboxPermissions.ExecuteDenied", "Denied")));
    }
}
