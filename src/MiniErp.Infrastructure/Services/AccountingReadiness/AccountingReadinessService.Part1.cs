using System.Data;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountingReadiness.AccountingReadinessErrors;

namespace MiniErp.Infrastructure.Services.AccountingReadiness;

public sealed partial class AccountingReadinessService
{
    public async Task<Result<AccountingReadinessResponse>> GetAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            LogEvents.ReadinessStarted,
            "Accounting readiness check started for company {CompanyId}, fiscal year {FiscalYearId}",
            companyId,
            fiscalYearId);

        try
        {
            var result = await GetCoreAsync(fiscalYearId, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    LogEvents.ReadinessFailed,
                    "Accounting readiness check failed for company {CompanyId}, fiscal year {FiscalYearId}, error count {ErrorCount}",
                    companyId,
                    fiscalYearId,
                    result.Errors.Count);
                RecordReadinessCheck("failed");
                RecordFailure("readiness", "result_failure");
                return result;
            }

            var value = result.Value;
            logger.LogInformation(
                LogEvents.ReadinessCompleted,
                "Accounting readiness check completed for company {CompanyId}, fiscal year {FiscalYearId}, ready {IsReady}, sources {SourceCount}, posted {PostedCount}, missing {MissingCount}, orphan journals {OrphanCount}, duplicate journals {DuplicateCount}, unbalanced journals {UnbalancedCount}, pending inventory costs {PendingCostCount}, mapping issues {MappingIssueCount}, deferred payroll sources {DeferredPayrollCount}",
                companyId,
                fiscalYearId,
                value.IsReady,
                value.TotalSources,
                value.PostedSources,
                value.MissingJournalSources,
                value.OrphanAutomaticJournals,
                value.DuplicateAutomaticJournals,
                value.UnbalancedAutomaticJournals,
                value.PendingInventoryCosts,
                value.MissingOrInvalidMappings,
                value.DeferredPayrollSources);
            RecordReadinessCheck(value.IsReady ? "ready" : "not_ready");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                LogEvents.ReadinessFailed,
                exception,
                "Accounting readiness check failed unexpectedly for company {CompanyId}, fiscal year {FiscalYearId}",
                companyId,
                fiscalYearId);
            RecordReadinessCheck("failed");
            RecordFailure("readiness", "unexpected");
            throw;
        }
    }

    private async Task<Result<AccountingReadinessResponse>> GetCoreAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var fiscalYearResult = await GetFiscalYearAsync(
            fiscalYearId,
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<AccountingReadinessResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYear = fiscalYearResult.Value;
        var sources = await LoadSourcesAsync(
            fiscalYear.Id,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            cancellationToken);
        var sourceKeys = sources
            .Select(source => source.Key)
            .ToHashSet();

        var automaticEntries = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.FiscalYearId == fiscalYear.Id &&
                entry.EntryType == JournalEntryType.Automatic &&
                entry.Status == JournalEntryStatus.Posted &&
                entry.ReversalOfEntryId == null &&
                entry.SourceType.HasValue &&
                entry.SourceId.HasValue)
            .Select(entry => new
            {
                entry.Id,
                SourceType = entry.SourceType!.Value,
                SourceId = entry.SourceId!.Value,
                entry.SourceNumber,
                entry.EntryDate,
                Debit = entry.Lines.Sum(line => line.Debit),
                Credit = entry.Lines.Sum(line => line.Credit),
                LineCount = entry.Lines.Count()
            })
            .ToListAsync(cancellationToken);
        var postedEntries = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.FiscalYearId == fiscalYear.Id &&
                entry.Status == JournalEntryStatus.Posted &&
                entry.ReversalOfEntryId == null)
            .Select(entry => new
            {
                entry.Id,
                entry.SourceType,
                entry.SourceId,
                entry.SourceNumber,
                entry.EntryNumber,
                entry.EntryDate,
                Debit = entry.Lines.Sum(line => line.Debit),
                Credit = entry.Lines.Sum(line => line.Credit),
                LineCount = entry.Lines.Count()
            })
            .ToListAsync(cancellationToken);
        var postedKeys = automaticEntries
            .Select(entry => new SourceKey(entry.SourceType, entry.SourceId))
            .ToHashSet();

        var issues = new List<AccountingReadinessIssue>();
        foreach (var source in sources.Where(source =>
                     !postedKeys.Contains(source.Key)))
        {
            issues.Add(CreateSourceIssue(
                "MissingJournal",
                source,
                "الحركة مؤثرة ماليًا ولم يتم إنشاء قيد تلقائي لها."));
        }

        var orphanEntries = automaticEntries
            .Where(entry =>
                entry.SourceType != JournalEntrySourceType.PayrollEntry &&
                !sourceKeys.Contains(new SourceKey(
                    entry.SourceType,
                    entry.SourceId)))
            .ToArray();
        foreach (var entry in orphanEntries)
        {
            issues.Add(new AccountingReadinessIssue(
                "OrphanJournal",
                entry.SourceType,
                entry.SourceId,
                entry.SourceNumber,
                entry.EntryDate,
                null,
                null,
                "القيد التلقائي لا يملك مصدرًا تشغيليًا مؤثرًا مطابقًا داخل السنة."));
        }

        var duplicateGroups = automaticEntries
            .GroupBy(entry => new SourceKey(
                entry.SourceType,
                entry.SourceId))
            .Where(group => group.Count() > 1)
            .ToArray();
        foreach (var group in duplicateGroups)
        {
            var first = group.First();
            issues.Add(new AccountingReadinessIssue(
                "DuplicateJournal",
                group.Key.SourceType,
                group.Key.SourceId,
                first.SourceNumber,
                first.EntryDate,
                null,
                null,
                $"يوجد {group.Count()} قيود أصلية لنفس المصدر."));
        }

        var unbalancedEntries = postedEntries
            .Where(entry => entry.LineCount == 0 || entry.Debit != entry.Credit)
            .ToArray();
        foreach (var entry in unbalancedEntries)
        {
            issues.Add(new AccountingReadinessIssue(
                "UnbalancedJournal",
                entry.SourceType,
                entry.SourceId,
                entry.SourceNumber ?? entry.EntryNumber,
                entry.EntryDate,
                null,
                null,
                entry.LineCount == 0
                    ? "القيد لا يحتوي على أي سطور."
                    : $"القيد غير متوازن: مدين {entry.Debit} ودائن {entry.Credit}."));
        }

        var pendingCosts = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYear.Id &&
                movement.MovementDate >= fiscalYear.StartDate &&
                movement.MovementDate <= fiscalYear.EndDate &&
                (movement.CostStatus == InventoryCostStatus.Pending ||
                 movement.CostStatus == InventoryCostStatus.PartiallyCosted))
            .Select(movement => new
            {
                movement.Id,
                movement.ReferenceId,
                movement.ReferenceNumber,
                movement.MovementDate,
                movement.MovementType,
                movement.CostStatus
            })
            .ToListAsync(cancellationToken);
        foreach (var movement in pendingCosts)
        {
            issues.Add(new AccountingReadinessIssue(
                "PendingInventoryCost",
                MapMovementSourceType(movement.MovementType),
                movement.ReferenceId,
                movement.ReferenceNumber,
                movement.MovementDate,
                null,
                null,
                $"تكلفة حركة المخزون ما زالت {movement.CostStatus}."));
        }

        var missingOpeningLines = await LoadMissingOpeningLinesAsync(
            fiscalYear.Id,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            cancellationToken);
        foreach (var line in missingOpeningLines)
        {
            issues.Add(new AccountingReadinessIssue(
                IssueType: "MissingInventoryMovement",
                SourceType: JournalEntrySourceType.StockOpeningBalance,
                SourceId: line.OpeningBalanceId,
                SourceNumber: line.DocumentNumber,
                SourceDate: line.DocumentDate,
                MappingType: AccountingMappingType.Inventory,
                MappingSourceId: null,
                Message: $"سطر الرصيد الافتتاحي للصنف {line.ItemId} لا يملك حركة OpeningBalance."));
        }

        var unresolvedCounts = await dbContext.InventoryCounts
            .AsNoTracking()
            .Where(count =>
                count.CompanyId == companyId &&
                count.FiscalYearId == fiscalYear.Id &&
                count.CountDate >= fiscalYear.StartDate &&
                count.CountDate <= fiscalYear.EndDate &&
                !count.ReconciledAt.HasValue &&
                count.Lines.Any(line =>
                    line.PhysicalQuantity.HasValue &&
                    line.PhysicalQuantity.Value != line.SystemQuantity))
            .Select(count => new
            {
                count.Id,
                count.DocumentNumber,
                count.CountDate
            })
            .ToListAsync(cancellationToken);
        foreach (var count in unresolvedCounts)
        {
            issues.Add(new AccountingReadinessIssue(
                IssueType: "UnresolvedDifference",
                SourceType: JournalEntrySourceType.InventoryCount,
                SourceId: count.Id,
                SourceNumber: count.DocumentNumber,
                SourceDate: count.CountDate,
                MappingType: null,
                MappingSourceId: null,
                Message: "يوجد جرد مخزني بفروقات لم تتم تسويتها بعد."));
        }

        var mappingIssues = await LoadMappingIssuesAsync(
            fiscalYear.Id,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            cancellationToken);
        issues.AddRange(mappingIssues);

        var deferredPayrollSources = await dbContext.PayrollEntries
            .AsNoTracking()
            .CountAsync(entry =>
                entry.CompanyId == companyId &&
                entry.FiscalYearId == fiscalYear.Id &&
                entry.EndDate >= fiscalYear.StartDate &&
                entry.EndDate <= fiscalYear.EndDate,
                cancellationToken);

        var sourceSummaries = sources
            .GroupBy(source => source.Key.SourceType)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var total = group.Count();
                var posted = group.Count(source =>
                    postedKeys.Contains(source.Key));
                return new AccountingReadinessSourceSummary(
                    group.Key,
                    total,
                    posted,
                    total - posted);
            })
            .ToArray();
        var postedSourceCount = sources.Count(source =>
            postedKeys.Contains(source.Key));
        var missingSourceCount = sources.Count - postedSourceCount;
        var isReady = missingSourceCount == 0 &&
            orphanEntries.Length == 0 &&
            duplicateGroups.Length == 0 &&
            unbalancedEntries.Length == 0 &&
            pendingCosts.Count == 0 &&
            missingOpeningLines.Count == 0 &&
            unresolvedCounts.Count == 0 &&
            mappingIssues.Count == 0;

        return Result<AccountingReadinessResponse>.Success(
            new AccountingReadinessResponse(
                fiscalYear.Id,
                fiscalYear.Name,
                fiscalYear.StartDate,
                fiscalYear.EndDate,
                isReady,
                sources.Count,
                postedSourceCount,
                missingSourceCount,
                orphanEntries.Length,
                duplicateGroups.Length,
                unbalancedEntries.Length,
                pendingCosts.Count,
                mappingIssues.Count,
                deferredPayrollSources,
                sourceSummaries,
                issues));
    }

    public async Task<Result<AccountingBackfillResponse>> BackfillAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            LogEvents.BackfillStarted,
            "Accounting backfill started for company {CompanyId}, fiscal year {FiscalYearId}",
            companyId,
            fiscalYearId);

        try
        {
            var result = await BackfillCoreAsync(fiscalYearId, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    LogEvents.BackfillFailed,
                    "Accounting backfill failed for company {CompanyId}, fiscal year {FiscalYearId}, error count {ErrorCount}",
                    companyId,
                    fiscalYearId,
                    result.Errors.Count);
                RecordBackfill("failed");
                RecordFailure("backfill", "result_failure");
                return result;
            }

            var value = result.Value;
            logger.LogInformation(
                LogEvents.BackfillCompleted,
                "Accounting backfill completed for company {CompanyId}, fiscal year {FiscalYearId}, sources {SourceCount}, created {CreatedCount}, updated {UpdatedCount}, deferred payroll sources {DeferredPayrollCount}, ready {IsReady}",
                companyId,
                fiscalYearId,
                value.ProcessedSources,
                value.CreatedJournals,
                value.UpdatedJournals,
                value.DeferredPayrollSources,
                value.Readiness.IsReady);
            RecordBackfill("completed");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                LogEvents.BackfillFailed,
                exception,
                "Accounting backfill failed unexpectedly for company {CompanyId}, fiscal year {FiscalYearId}",
                companyId,
                fiscalYearId);
            RecordBackfill("failed");
            RecordFailure("backfill", "unexpected");
            throw;
        }
    }

    private async Task<Result<AccountingBackfillResponse>> BackfillCoreAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var fiscalYearResult = await GetFiscalYearAsync(
            fiscalYearId,
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<AccountingBackfillResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYear = fiscalYearResult.Value;
        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<AccountingBackfillResponse>.Failure(
                FiscalYearClosed(fiscalYear.Name));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (defaultAccountingSetupService is not null)
        {
            await defaultAccountingSetupService.EnsureFiscalYearAsync(
                companyId,
                fiscalYear.Id,
                cancellationToken);
        }

        var missingOpeningLines = await LoadMissingOpeningLinesAsync(
            fiscalYear.Id,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            cancellationToken);
        foreach (var line in missingOpeningLines)
        {
            dbContext.ItemMovements.Add(new ItemMovement
            {
                CompanyId = companyId,
                StoreId = line.StoreId,
                ItemId = line.ItemId,
                ItemUnitId = line.ItemUnitId,
                MovementType = ItemMovementType.OpeningBalance,
                ReferenceId = line.OpeningBalanceId,
                ReferenceNumber = line.DocumentNumber,
                MovementDate = line.DocumentDate,
                QuantityIn = line.Quantity,
                QuantityOut = 0m,
                Description = $"Opening balance {line.DocumentNumber}"
            });
        }
        if (missingOpeningLines.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Repair only obsolete stock-document journals. The accounting fiscal-year
        // opening entry remains the sole posting for these generated documents.
        var carriedOpeningIds = await dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYear.Id &&
                (balance.DocumentNumber.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.DocumentNumberPrefix) ||
                 (balance.Notes != null && balance.Notes.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.MarkerPrefix))))
            .Select(balance => balance.Id)
            .ToListAsync(cancellationToken);
        foreach (var openingId in carriedOpeningIds)
        {
            var cleanup = await inventoryPostingService.SynchronizeStockOpeningBalanceAsync(
                openingId, cancellationToken);
            if (cleanup.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<AccountingBackfillResponse>.Failure(cleanup.Errors);
            }
        }

        var sources = await LoadSourcesAsync(
            fiscalYear.Id,
            fiscalYear.StartDate,
            fiscalYear.EndDate,
            cancellationToken);
        var existingKeys = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.FiscalYearId == fiscalYear.Id &&
                entry.EntryType == JournalEntryType.Automatic &&
                entry.Status == JournalEntryStatus.Posted &&
                entry.ReversalOfEntryId == null &&
                entry.SourceType.HasValue &&
                entry.SourceId.HasValue)
            .Select(entry => new SourceKey(
                entry.SourceType!.Value,
                entry.SourceId!.Value))
            .ToListAsync(cancellationToken);
        var existingKeySet = existingKeys.ToHashSet();

        var costingKeys = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYear.Id &&
                movement.MovementDate >= fiscalYear.StartDate &&
                movement.MovementDate <= fiscalYear.EndDate)
            .Select(movement => new InventoryCostingKey(
                movement.StoreId,
                movement.ItemId,
                movement.FiscalYearId))
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var costingError = await inventoryCostingService.RecalculateAsync(
            costingKeys,
            cancellationToken);
        if (costingError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<AccountingBackfillResponse>.Failure(costingError);
        }

        var created = 0;
        var updated = 0;
        foreach (var source in sources
                     .OrderBy(source => source.SourceDate)
                     .ThenBy(source => source.Key.SourceType)
                     .ThenBy(source => source.Key.SourceId))
        {
            if (IsImmutableRevaluation(source.Key.SourceType) &&
                existingKeySet.Contains(source.Key))
            {
                continue;
            }

            var postingResult = await SynchronizeAsync(
                source,
                cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<AccountingBackfillResponse>.Failure(
                    postingResult.Errors);
            }

            if (existingKeySet.Contains(source.Key))
            {
                updated++;
            }
            else
            {
                created++;
                existingKeySet.Add(source.Key);
            }
        }

        var readinessResult = await GetAsync(
            fiscalYearId,
            cancellationToken);
        if (readinessResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<AccountingBackfillResponse>.Failure(
                readinessResult.Errors);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result<AccountingBackfillResponse>.Success(
            new AccountingBackfillResponse(
                fiscalYear.Id,
                sources.Count,
                created,
                updated,
                readinessResult.Value.DeferredPayrollSources,
                readinessResult.Value));
    }
}
