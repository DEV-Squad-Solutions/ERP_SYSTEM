using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed class OpeningBalancePostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : IOpeningBalancePostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> SynchronizeCashboxAsync(
        int cashboxId,
        CancellationToken cancellationToken = default)
    {
        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == cashboxId)
            .Select(entity => new
            {
                entity.Id,
                entity.Code,
                entity.Name,
                entity.OpeningBalanceDate,
                entity.BaseOpeningBalance
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (cashbox is null)
        {
            return Result.Failure(
                MiniErp.Application.Features.Cashboxes.CashboxErrors.NotFound(
                    cashboxId));
        }

        if (cashbox.BaseOpeningBalance == 0m)
        {
            return await DeleteAsync(
                JournalEntrySourceType.CashboxOpeningBalance,
                cashbox.Id,
                cancellationToken);
        }

        var fiscalYearResult = await ResolveFiscalYearAsync(
            cashbox.OpeningBalanceDate,
            nameof(cashbox.OpeningBalanceDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        var cashboxAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            AccountingMappingType.Cashbox,
            cashbox.Id,
            cancellationToken);
        if (cashboxAccountResult.IsFailure)
        {
            return Result.Failure(cashboxAccountResult.Errors);
        }

        var equityResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            AccountingMappingType.OpeningBalanceEquity,
            cancellationToken: cancellationToken);
        if (equityResult.IsFailure)
        {
            return Result.Failure(equityResult.Errors);
        }

        var amount = Math.Abs(cashbox.BaseOpeningBalance);
        var cashboxIsDebit = cashbox.BaseOpeningBalance > 0m;
        var postingResult = await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYearResult.Value,
                EntryDate: cashbox.OpeningBalanceDate,
                Description: $"رصيد افتتاحي خزينة {cashbox.Name}",
                SourceType: JournalEntrySourceType.CashboxOpeningBalance,
                SourceId: cashbox.Id,
                SourceNumber: cashbox.Code,
                Lines:
                [
                    new JournalEntryLineRequest(
                        cashboxAccountResult.Value,
                        "رصيد الخزينة الافتتاحي",
                        cashboxIsDebit ? amount : 0m,
                        cashboxIsDebit ? 0m : amount),
                    new JournalEntryLineRequest(
                        equityResult.Value,
                        "مقابل رصيد الخزينة الافتتاحي",
                        cashboxIsDebit ? 0m : amount,
                        cashboxIsDebit ? amount : 0m)
                ]),
            cancellationToken);
        return postingResult.IsFailure
            ? Result.Failure(postingResult.Errors)
            : Result.Success();
    }

    public async Task<Result> SynchronizePartnerAsync(
        int openingBalanceId,
        CancellationToken cancellationToken = default)
    {
        var balance = await dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == openingBalanceId)
            .Select(entity => new
            {
                entity.Id,
                entity.DocumentNumber,
                entity.DocumentDate,
                entity.BalanceType,
                entity.BaseAmount
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (balance is null)
        {
            return Result.Failure(
                PartnerOpeningBalanceErrors.NotFound(openingBalanceId));
        }

        var fiscalYearResult = await ResolveFiscalYearAsync(
            balance.DocumentDate,
            nameof(balance.DocumentDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        var isDebit = balance.BalanceType == PartnerBalanceType.Receivable;
        var controlResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            isDebit
                ? AccountingMappingType.CustomerControl
                : AccountingMappingType.SupplierControl,
            cancellationToken: cancellationToken);
        return await SynchronizeAsync(
            fiscalYearResult.Value,
            balance.DocumentDate,
            balance.DocumentNumber,
            JournalEntrySourceType.PartnerOpeningBalance,
            balance.Id,
            balance.BaseAmount,
            isDebit,
            controlResult,
            "رصيد افتتاحي طرف",
            cancellationToken);
    }

    public async Task<Result> SynchronizeEmployeeAsync(
        int openingBalanceId,
        CancellationToken cancellationToken = default)
    {
        var balance = await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == openingBalanceId)
            .Select(entity => new
            {
                entity.Id,
                entity.PayrollEntryId,
                entity.DocumentNumber,
                entity.DocumentDate,
                entity.BalanceType,
                entity.BaseAmount
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (balance is null)
        {
            return Result.Failure(
                EmployeeOpeningBalanceErrors.NotFound(openingBalanceId));
        }

        // Payroll-generated balances are represented by the payroll source
        // itself, so they must not create a second opening-balance entry.
        if (balance.PayrollEntryId.HasValue)
        {
            return await DeleteAsync(
                JournalEntrySourceType.EmployeeOpeningBalance,
                balance.Id,
                cancellationToken);
        }

        var fiscalYearResult = await ResolveFiscalYearAsync(
            balance.DocumentDate,
            nameof(balance.DocumentDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        var isDebit = balance.BalanceType == EmployeeBalanceType.Debit;
        var controlResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            isDebit
                ? AccountingMappingType.EmployeeReceivable
                : AccountingMappingType.EmployeeControl,
            cancellationToken: cancellationToken);
        return await SynchronizeAsync(
            fiscalYearResult.Value,
            balance.DocumentDate,
            balance.DocumentNumber,
            JournalEntrySourceType.EmployeeOpeningBalance,
            balance.Id,
            balance.BaseAmount,
            isDebit,
            controlResult,
            "رصيد افتتاحي موظف",
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            sourceType,
            sourceId,
            cancellationToken);

    private async Task<Result> SynchronizeAsync(
        int fiscalYearId,
        DateOnly documentDate,
        string documentNumber,
        JournalEntrySourceType sourceType,
        int sourceId,
        decimal amount,
        bool controlIsDebit,
        Result<int> controlResult,
        string description,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return await DeleteAsync(
                sourceType,
                sourceId,
                cancellationToken);
        }

        if (controlResult.IsFailure)
        {
            return Result.Failure(controlResult.Errors);
        }

        var equityResult = await accountMappingResolver.ResolveAsync(
            fiscalYearId,
            AccountingMappingType.OpeningBalanceEquity,
            cancellationToken: cancellationToken);
        if (equityResult.IsFailure)
        {
            return Result.Failure(equityResult.Errors);
        }

        var postingResult = await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYearId,
                EntryDate: documentDate,
                Description: $"{description} {documentNumber}",
                SourceType: sourceType,
                SourceId: sourceId,
                SourceNumber: documentNumber,
                Lines:
                [
                    new JournalEntryLineRequest(
                        controlResult.Value,
                        description,
                        controlIsDebit ? amount : 0m,
                        controlIsDebit ? 0m : amount),
                    new JournalEntryLineRequest(
                        equityResult.Value,
                        "مقابل الرصيد الافتتاحي",
                        controlIsDebit ? 0m : amount,
                        controlIsDebit ? amount : 0m)
                ]),
            cancellationToken);
        return postingResult.IsFailure
            ? Result.Failure(postingResult.Errors)
            : Result.Success();
    }

    private async Task<Result<int>> ResolveFiscalYearAsync(
        DateOnly documentDate,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= documentDate &&
                year.EndDate >= documentDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result<int>.Failure(
                FiscalYearErrors.DateNotCovered(documentDate, propertyName));
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result<int>.Success(fiscalYear.Id)
            : Result<int>.Failure(
                FiscalYearErrors.Closed(
                    documentDate,
                    fiscalYear.Name,
                    propertyName));
    }
}
