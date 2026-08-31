using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountStatementMappings;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountStatementMappings.AccountStatementMappingErrors;

namespace MiniErp.Infrastructure.Services.AccountStatementMappings;

public sealed class AccountStatementMappingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IAccountStatementMappingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<IReadOnlyList<AccountStatementMappingResponse>>> GetAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default)
    {
        var scopeValidation = await ValidateScopeAsync(
            fiscalYearId,
            statementType,
            requireOpen: false,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<IReadOnlyList<AccountStatementMappingResponse>>.Failure(
                scopeValidation.Errors);
        }

        var response = await LoadResponsesAsync(
            fiscalYearId,
            statementType,
            cancellationToken);
        return Result<IReadOnlyList<AccountStatementMappingResponse>>.Success(
            response);
    }

    public async Task<Result<IReadOnlyList<AccountStatementMappingResponse>>> ReplaceAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        ReplaceAccountStatementMappingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var scopeValidation = await ValidateScopeAsync(
            fiscalYearId,
            statementType,
            requireOpen: true,
            cancellationToken);
        if (scopeValidation.IsFailure)
        {
            return Result<IReadOnlyList<AccountStatementMappingResponse>>.Failure(
                scopeValidation.Errors);
        }

        var rows = request.Mappings;
        var errors = new List<Error>();
        var seenAccountIds = new HashSet<int>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!seenAccountIds.Add(row.AccountId))
            {
                errors.Add(DuplicateAccount(row.AccountId, index));
            }
        }

        var accountIds = rows.Select(row => row.AccountId).Distinct().ToArray();
        var lineIds = rows
            .Select(row => row.FinancialStatementLineId)
            .Distinct()
            .ToArray();

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                accountIds.Contains(account.Id))
            .Select(account => new
            {
                account.Id,
                account.AccountType,
                account.IsPosting,
                account.IsActive
            })
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        var lines = await dbContext.FinancialStatementLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.FiscalYearId == fiscalYearId &&
                line.StatementType == statementType &&
                lineIds.Contains(line.Id))
            .Select(line => new
            {
                line.Id,
                line.IsAssignable,
                line.IsActive
            })
            .ToDictionaryAsync(line => line.Id, cancellationToken);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!accounts.TryGetValue(row.AccountId, out var account))
            {
                errors.Add(AccountNotFound(row.AccountId, index));
            }
            else
            {
                if (!account.IsActive || !account.IsPosting)
                {
                    errors.Add(AccountNotPostingOrInactive(row.AccountId, index));
                }
                else if (!IsCompatible(account.AccountType, statementType))
                {
                    errors.Add(IncompatibleAccountType(row.AccountId, index));
                }
            }

            if (!lines.TryGetValue(row.FinancialStatementLineId, out var line))
            {
                errors.Add(LineNotFound(row.FinancialStatementLineId, index));
            }
            else if (!line.IsActive || !line.IsAssignable)
            {
                errors.Add(LineNotAssignable(row.FinancialStatementLineId, index));
            }
        }

        if (errors.Count > 0)
        {
            return Result<IReadOnlyList<AccountStatementMappingResponse>>.Failure(
                errors);
        }

        var existingMappings = await dbContext.AccountStatementMappings
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                mapping.StatementType == statementType)
            .ToListAsync(cancellationToken);

        try
        {
            if (existingMappings.Count > 0)
            {
                dbContext.AccountStatementMappings.RemoveRange(existingMappings);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var mappings = rows.Select(row => new AccountStatementMapping
            {
                CompanyId = companyId,
                FiscalYearId = fiscalYearId,
                StatementType = statementType,
                AccountId = row.AccountId,
                FinancialStatementLineId = row.FinancialStatementLineId
            });
            dbContext.AccountStatementMappings.AddRange(mappings);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = await LoadResponsesAsync(
                fiscalYearId,
                statementType,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<AccountStatementMappingResponse>>.Success(
                response);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<IReadOnlyList<AccountStatementMappingResponse>>.Failure(
                ReplaceConflict());
        }
    }

    private async Task<IReadOnlyList<AccountStatementMappingResponse>>
        LoadResponsesAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AccountStatementMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                mapping.StatementType == statementType)
            .OrderBy(mapping => mapping.Account.Code)
            .ThenBy(mapping => mapping.Account.Id)
            .Select(mapping => new AccountStatementMappingRow
            {
                Id = mapping.Id,
                FiscalYearId = mapping.FiscalYearId,
                StatementType = mapping.StatementType,
                AccountId = mapping.AccountId,
                AccountCode = mapping.Account.Code,
                AccountName = mapping.Account.Name,
                AccountType = mapping.Account.AccountType,
                FinancialStatementLineId = mapping.FinancialStatementLineId,
                FinancialStatementLineCode = mapping.FinancialStatementLine.Code,
                FinancialStatementLineName = mapping.FinancialStatementLine.Name
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AccountStatementMappingResponse(
            Id: row.Id,
            FiscalYearId: row.FiscalYearId,
            StatementType: row.StatementType,
            AccountId: row.AccountId,
            AccountCode: row.AccountCode,
            AccountName: row.AccountName,
            AccountType: row.AccountType,
            FinancialStatementLineId: row.FinancialStatementLineId,
            FinancialStatementLineCode: row.FinancialStatementLineCode,
            FinancialStatementLineName: row.FinancialStatementLineName))
            .ToList();
    }

    private async Task<Result> ValidateScopeAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        bool requireOpen,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(statementType))
        {
            return Result.Failure(InvalidStatementType(statementType));
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year => year.CompanyId == companyId && year.Id == fiscalYearId)
            .Select(year => new { year.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(FiscalYearNotFound(fiscalYearId));
        }

        return requireOpen && fiscalYear.Status == FiscalYearStatus.Closed
            ? Result.Failure(FiscalYearClosed())
            : Result.Success();
    }

    private static bool IsCompatible(
        AccountType accountType,
        FinancialStatementType statementType) =>
        statementType switch
        {
            FinancialStatementType.FinancialPosition => accountType is
                AccountType.Asset or
                AccountType.Liability or
                AccountType.Equity,
            FinancialStatementType.IncomeStatement => accountType is
                AccountType.Revenue or
                AccountType.Expense,
            FinancialStatementType.CashFlow => true,
            _ => false
        };

    private sealed class AccountStatementMappingRow
    {
        public int Id { get; init; }

        public int FiscalYearId { get; init; }

        public FinancialStatementType StatementType { get; init; }

        public int AccountId { get; init; }

        public string AccountCode { get; init; } = string.Empty;

        public string AccountName { get; init; } = string.Empty;

        public AccountType AccountType { get; init; }

        public int FinancialStatementLineId { get; init; }

        public string FinancialStatementLineCode { get; init; } = string.Empty;

        public string FinancialStatementLineName { get; init; } = string.Empty;
    }
}
