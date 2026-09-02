using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountMappings.AccountMappingErrors;

namespace MiniErp.Infrastructure.Services.AccountMappings;

public sealed class AccountMappingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IAccountMappingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<IReadOnlyList<AccountMappingResponse>>> GetAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYearValidation = await ValidateFiscalYearAsync(
            fiscalYearId,
            requireOpen: false,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<IReadOnlyList<AccountMappingResponse>>.Failure(
                fiscalYearValidation.Errors);
        }

        return Result<IReadOnlyList<AccountMappingResponse>>.Success(
            await LoadResponsesAsync(fiscalYearId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<AccountMappingResponse>>> ReplaceAsync(
        int fiscalYearId,
        ReplaceAccountMappingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var fiscalYearValidation = await ValidateFiscalYearAsync(
            fiscalYearId,
            requireOpen: true,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<IReadOnlyList<AccountMappingResponse>>.Failure(
                fiscalYearValidation.Errors);
        }

        var rows = request.Mappings;
        var errors = new List<Error>();
        var seen = new HashSet<(AccountingMappingType MappingType, int? SourceId)>();
        var mappingTypes = Enum.GetValues<AccountingMappingType>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!mappingTypes.Contains(row.MappingType))
            {
                errors.Add(InvalidMappingType(row.MappingType, index));
                continue;
            }

            var requiresSource = row.MappingType is
                AccountingMappingType.Cashbox or
                AccountingMappingType.CashMovementType;

            if (requiresSource && !row.SourceId.HasValue)
            {
                errors.Add(SourceRequired(index));
            }
            else if (!requiresSource && row.SourceId.HasValue)
            {
                errors.Add(SourceNotAllowed(index));
            }

            if (!seen.Add((row.MappingType, row.SourceId)))
            {
                errors.Add(DuplicateMapping(index));
            }
        }

        var accountIds = rows.Select(row => row.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                accountIds.Contains(account.Id))
            .Select(account => new
            {
                account.Id,
                account.AccountType,
                account.IsActive,
                account.IsPosting,
                account.ParentAccountId
            })
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        var cashboxIds = rows
            .Where(row => row.MappingType == AccountingMappingType.Cashbox &&
                          row.SourceId.HasValue)
            .Select(row => row.SourceId!.Value)
            .Distinct()
            .ToArray();
        var movementTypeIds = rows
            .Where(row => row.MappingType == AccountingMappingType.CashMovementType &&
                          row.SourceId.HasValue)
            .Select(row => row.SourceId!.Value)
            .Distinct()
            .ToArray();

        var cashboxSet = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashboxIds.Contains(cashbox.Id))
            .Select(cashbox => cashbox.Id)
            .ToHashSetAsync(cancellationToken);
        var movementTypeClassifications = await dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId &&
                movementTypeIds.Contains(movementType.Id))
            .Select(movementType => new
            {
                movementType.Id,
                movementType.Classification
            })
            .ToDictionaryAsync(
                movementType => movementType.Id,
                movementType => movementType.Classification,
                cancellationToken);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.SourceId is { } sourceId)
            {
                var sourceExists = row.MappingType switch
                {
                    AccountingMappingType.Cashbox => cashboxSet.Contains(sourceId),
                    AccountingMappingType.CashMovementType =>
                        movementTypeClassifications.ContainsKey(sourceId),
                    _ => false
                };

                if (!sourceExists)
                {
                    errors.Add(SourceNotFound(row.MappingType, sourceId, index));
                }
            }

            if (!accounts.TryGetValue(row.AccountId, out var account))
            {
                errors.Add(AccountNotFound(row.AccountId, index));
            }
            else if (!account.IsActive ||
                     !account.IsPosting ||
                     !account.ParentAccountId.HasValue)
            {
                errors.Add(AccountNotPostingOrInactive(row.AccountId, index));
            }
            else if (!IsCompatible(
                         row.MappingType,
                         account.AccountType,
                         row.SourceId is { } sourceForClassification &&
                         movementTypeClassifications.TryGetValue(
                             sourceForClassification,
                             out var classification)
                             ? classification
                             : null))
            {
                errors.Add(IncompatibleAccountType(
                    row.MappingType,
                    row.AccountId,
                    index));
            }
        }

        if (errors.Count > 0)
        {
            return Result<IReadOnlyList<AccountMappingResponse>>.Failure(errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existingMappings = await dbContext.Set<AccountMapping>()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId)
            .ToListAsync(cancellationToken);

        try
        {
            if (existingMappings.Count > 0)
            {
                dbContext.Set<AccountMapping>().RemoveRange(existingMappings);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            dbContext.Set<AccountMapping>().AddRange(rows.Select(row =>
                new AccountMapping
                {
                    CompanyId = companyId,
                    FiscalYearId = fiscalYearId,
                    MappingType = row.MappingType,
                    SourceId = row.SourceId,
                    AccountId = row.AccountId
                }));
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = await LoadResponsesAsync(
                fiscalYearId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<AccountMappingResponse>>.Success(response);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<IReadOnlyList<AccountMappingResponse>>.Failure(
                ReplaceConflict());
        }
    }

    private async Task<IReadOnlyList<AccountMappingResponse>> LoadResponsesAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<AccountMapping>()
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId)
            .OrderBy(mapping => mapping.MappingType)
            .ThenBy(mapping => mapping.SourceId)
            .ThenBy(mapping => mapping.Account.Code)
            .Select(mapping => new MappingRow
            {
                Id = mapping.Id,
                FiscalYearId = mapping.FiscalYearId,
                FiscalYearName = mapping.FiscalYear.Name,
                MappingType = mapping.MappingType,
                SourceId = mapping.SourceId,
                AccountId = mapping.AccountId,
                AccountCode = mapping.Account.Code,
                AccountName = mapping.Account.Name,
                AccountType = mapping.Account.AccountType,
                RowVersion = mapping.RowVersion
            })
            .ToListAsync(cancellationToken);

        var cashboxIds = rows
            .Where(row => row.MappingType == AccountingMappingType.Cashbox &&
                          row.SourceId.HasValue)
            .Select(row => row.SourceId!.Value)
            .Distinct()
            .ToArray();
        var movementTypeIds = rows
            .Where(row => row.MappingType == AccountingMappingType.CashMovementType &&
                          row.SourceId.HasValue)
            .Select(row => row.SourceId!.Value)
            .Distinct()
            .ToArray();

        var cashboxes = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashboxIds.Contains(cashbox.Id))
            .Select(cashbox => new SourceRow
            {
                Id = cashbox.Id,
                Code = cashbox.Code,
                Name = cashbox.Name
            })
            .ToDictionaryAsync(source => source.Id, cancellationToken);
        var movementTypes = await dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId &&
                movementTypeIds.Contains(movementType.Id))
            .Select(movementType => new SourceRow
            {
                Id = movementType.Id,
                Code = null,
                Name = movementType.Name
            })
            .ToDictionaryAsync(source => source.Id, cancellationToken);

        return rows.Select(row =>
        {
            var source = row.MappingType == AccountingMappingType.Cashbox &&
                         row.SourceId.HasValue &&
                         cashboxes.TryGetValue(row.SourceId.Value, out var cashbox)
                ? cashbox
                : row.MappingType == AccountingMappingType.CashMovementType &&
                  row.SourceId.HasValue &&
                  movementTypes.TryGetValue(row.SourceId.Value, out var movementType)
                    ? movementType
                    : null;

            return new AccountMappingResponse(
                Id: row.Id,
                FiscalYearId: row.FiscalYearId,
                FiscalYearName: row.FiscalYearName,
                MappingType: row.MappingType,
                SourceId: row.SourceId,
                SourceCode: source?.Code,
                SourceName: source?.Name,
                AccountId: row.AccountId,
                AccountCode: row.AccountCode,
                AccountName: row.AccountName,
                AccountType: row.AccountType,
                RowVersion: row.RowVersion);
        }).ToList();
    }

    private async Task<Result> ValidateFiscalYearAsync(
        int fiscalYearId,
        bool requireOpen,
        CancellationToken cancellationToken)
    {
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
        AccountingMappingType mappingType,
        AccountType accountType,
        CashMovementClassification? movementClassification = null) =>
        mappingType switch
        {
            AccountingMappingType.Cashbox or
            AccountingMappingType.Inventory or
            AccountingMappingType.CustomerControl or
            AccountingMappingType.EmployeeReceivable =>
                accountType == AccountType.Asset,
            AccountingMappingType.SupplierControl or
            AccountingMappingType.EmployeeControl or
            AccountingMappingType.DriverControl =>
                accountType == AccountType.Liability,
            AccountingMappingType.Sales or
            AccountingMappingType.SalesReturn or
            AccountingMappingType.ExchangeGain or
            AccountingMappingType.InventoryAdjustmentGain =>
                accountType == AccountType.Revenue,
            AccountingMappingType.CostOfGoodsSold or
            AccountingMappingType.ExchangeLoss or
            AccountingMappingType.InventoryAdjustmentLoss or
            AccountingMappingType.DriverTripExpense =>
                accountType == AccountType.Expense,
            AccountingMappingType.OpeningBalanceEquity =>
                accountType == AccountType.Equity,
            AccountingMappingType.Purchase =>
                accountType is AccountType.Asset or AccountType.Expense,
            AccountingMappingType.PurchaseReturn =>
                accountType is AccountType.Asset or AccountType.Revenue,
            AccountingMappingType.CashMovementType => movementClassification switch
            {
                CashMovementClassification.Expense => accountType == AccountType.Expense,
                CashMovementClassification.Revenue => accountType == AccountType.Revenue,
                CashMovementClassification.PartnerSettlement => accountType is
                    AccountType.Asset or AccountType.Liability,
                CashMovementClassification.Other => accountType is
                    AccountType.Asset or
                    AccountType.Liability or
                    AccountType.Revenue or
                    AccountType.Expense,
                _ => false
            },
            _ => false
        };

    private sealed class MappingRow
    {
        public int Id { get; init; }

        public int FiscalYearId { get; init; }

        public string FiscalYearName { get; init; } = string.Empty;

        public AccountingMappingType MappingType { get; init; }

        public int? SourceId { get; init; }

        public int AccountId { get; init; }

        public string AccountCode { get; init; } = string.Empty;

        public string AccountName { get; init; } = string.Empty;

        public AccountType AccountType { get; init; }

        public byte[] RowVersion { get; init; } = [];
    }

    private sealed class SourceRow
    {
        public int Id { get; init; }

        public string? Code { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
