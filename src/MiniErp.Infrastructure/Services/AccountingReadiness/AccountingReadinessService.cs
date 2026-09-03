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
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountingReadiness.AccountingReadinessErrors;

namespace MiniErp.Infrastructure.Services.AccountingReadiness;

public sealed class AccountingReadinessService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryCostingService inventoryCostingService,
    IInvoicePostingService invoicePostingService,
    ICashVoucherPostingService cashVoucherPostingService,
    ICashboxTransferPostingService cashboxTransferPostingService,
    IInventoryPostingService inventoryPostingService,
    IOpeningBalancePostingService openingBalancePostingService,
    IDriverTripPostingService driverTripPostingService,
    ILogger<AccountingReadinessService> logger,
    IDefaultAccountingSetupService? defaultAccountingSetupService = null)
    : IAccountingReadinessService
{
    private const string MeterName = "MiniErp.Accounting";
    private static readonly Meter AccountingMeter = new(MeterName);
    private static readonly Counter<long> ReadinessChecks =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.checks",
            unit: "{check}",
            description: "Accounting readiness check outcomes.");
    private static readonly Counter<long> Backfills =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.backfills",
            unit: "{backfill}",
            description: "Accounting backfill outcomes.");
    private static readonly Counter<long> ReadinessFailures =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.failures",
            unit: "{failure}",
            description: "Accounting readiness and backfill failures.");
    private readonly int companyId = currentCompanyContext.CompanyId;

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

        var unresolvedCounts = await dbContext.InventoryCounts
            .AsNoTracking()
            .Where(count =>
                count.CompanyId == companyId &&
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

        var sources = await LoadSourcesAsync(
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
                movement.MovementDate >= fiscalYear.StartDate &&
                movement.MovementDate <= fiscalYear.EndDate)
            .Select(movement => new InventoryCostingKey(
                movement.StoreId,
                movement.ItemId))
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

    private async Task<Result> SynchronizeAsync(
        SourceDescriptor source,
        CancellationToken cancellationToken)
    {
        switch (source.Key.SourceType)
        {
            case JournalEntrySourceType.Invoice:
                return ToResult(await invoicePostingService.SynchronizeAsync(
                    source.Key.SourceId,
                    cancellationToken));

            case JournalEntrySourceType.CashVoucher:
                var voucher = await dbContext.CashVouchers
                    .SingleAsync(
                        entity =>
                            entity.CompanyId == companyId &&
                            entity.Id == source.Key.SourceId,
                        cancellationToken);
                return ToResult(await cashVoucherPostingService.SynchronizeAsync(
                    voucher,
                    cancellationToken));

            case JournalEntrySourceType.CashboxTransfer:
                return ToResult(await cashboxTransferPostingService
                    .SynchronizeAsync(source.Key.SourceId, cancellationToken));

            case JournalEntrySourceType.StockAdjustment:
                return await inventoryPostingService
                    .SynchronizeStockAdjustmentAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.StockOpeningBalance:
                return await inventoryPostingService
                    .SynchronizeStockOpeningBalanceAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.PartnerOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizePartnerAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.EmployeeOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizeEmployeeAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.CashboxOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizeCashboxAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.DriverTrip:
                return await driverTripPostingService.SynchronizeAsync(
                    source.Key.SourceId,
                    cancellationToken);

            default:
                return Result.Success();
        }
    }

    private static Result ToResult<T>(Result<T> result) =>
        result.IsFailure
            ? Result.Failure(result.Errors)
            : Result.Success();

    private async Task<List<SourceDescriptor>> LoadSourcesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var sources = new List<SourceDescriptor>();

        sources.AddRange(await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.InvoiceDate >= startDate &&
                invoice.InvoiceDate <= endDate)
            .Select(invoice => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.Invoice, invoice.Id),
                invoice.InvoiceNumber,
                invoice.InvoiceDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue &&
                voucher.VoucherDate >= startDate &&
                voucher.VoucherDate <= endDate)
            .Select(voucher => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.CashVoucher, voucher.Id),
                voucher.VoucherNumber,
                voucher.VoucherDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.TransferDate >= startDate &&
                transfer.TransferDate <= endDate)
            .Select(transfer => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.CashboxTransfer,
                    transfer.Id),
                transfer.TransferNumber,
                transfer.TransferDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.StockAdjustments
            .AsNoTracking()
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.DocumentDate >= startDate &&
                adjustment.DocumentDate <= endDate &&
                dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.ReferenceId == adjustment.Id &&
                    (movement.MovementType ==
                        ItemMovementType.AdjustmentIncrease ||
                     movement.MovementType ==
                        ItemMovementType.AdjustmentDecrease) &&
                    movement.TotalCost > 0m))
            .Select(adjustment => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.StockAdjustment,
                    adjustment.Id),
                adjustment.DocumentNumber,
                adjustment.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate &&
                dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.ReferenceId == balance.Id &&
                    movement.MovementType ==
                        ItemMovementType.OpeningBalance &&
                    movement.TotalCost > 0m))
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.StockOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.BaseAmount != 0m &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate)
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.PartnerOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                !balance.PayrollEntryId.HasValue &&
                balance.BaseAmount != 0m &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate)
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.EmployeeOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.BaseOpeningBalance != 0m &&
                cashbox.OpeningBalanceDate >= startDate &&
                cashbox.OpeningBalanceDate <= endDate)
            .Select(cashbox => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.CashboxOpeningBalance,
                    cashbox.Id),
                cashbox.Code,
                cashbox.OpeningBalanceDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.Cost.HasValue &&
                trip.Cost.Value > 0m &&
                trip.TripDate >= startDate &&
                trip.TripDate <= endDate)
            .Select(trip => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.DriverTrip, trip.Id),
                trip.InvoiceNumber,
                trip.TripDate))
            .ToListAsync(cancellationToken));

        return sources
            .DistinctBy(source => source.Key)
            .ToList();
    }

    private async Task<List<AccountingReadinessIssue>> LoadMappingIssuesAsync(
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var requirements = await LoadMappingRequirementsAsync(
            startDate,
            endDate,
            cancellationToken);
        if (requirements.Count == 0)
        {
            return [];
        }

        var mappings = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId)
            .Select(mapping => new
            {
                mapping.MappingType,
                mapping.SourceId,
                mapping.AccountId,
                mapping.Account.IsActive,
                mapping.Account.IsPosting
            })
            .ToListAsync(cancellationToken);

        var issues = new List<AccountingReadinessIssue>();
        foreach (var requirement in requirements.Distinct())
        {
            var mapping = mappings.FirstOrDefault(candidate =>
                candidate.MappingType == requirement.MappingType &&
                candidate.SourceId == requirement.SourceId);
            if (mapping is not null && mapping.IsActive && mapping.IsPosting)
            {
                continue;
            }

            issues.Add(new AccountingReadinessIssue(
                mapping is null ? "MissingMapping" : "InvalidMapping",
                requirement.SourceType,
                requirement.SourceDocumentId,
                requirement.SourceNumber,
                requirement.SourceDate,
                requirement.MappingType,
                requirement.SourceId,
                mapping is null
                    ? $"الربط المحاسبي {requirement.MappingType} غير موجود."
                    : $"الحساب المربوط بـ {requirement.MappingType} غير فعال أو غير قابل للتسجيل."));
        }

        return issues;
    }

    private async Task<List<MappingRequirement>> LoadMappingRequirementsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var requirements = new List<MappingRequirement>();
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.InvoiceDate >= startDate &&
                invoice.InvoiceDate <= endDate)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.InvoiceType,
                HasCost = dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.ReferenceId == invoice.Id &&
                    movement.TotalCost > 0m &&
                    (movement.MovementType == ItemMovementType.Sales ||
                     movement.MovementType == ItemMovementType.SalesReturn))
            })
            .ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            var (invoiceMapping, controlMapping) = invoice.InvoiceType switch
            {
                InvoiceType.Sales => (
                    AccountingMappingType.Sales,
                    AccountingMappingType.CustomerControl),
                InvoiceType.SalesReturn => (
                    AccountingMappingType.SalesReturn,
                    AccountingMappingType.CustomerControl),
                InvoiceType.Purchase => (
                    AccountingMappingType.Purchase,
                    AccountingMappingType.SupplierControl),
                _ => (
                    AccountingMappingType.PurchaseReturn,
                    AccountingMappingType.SupplierControl)
            };
            requirements.Add(MappingRequirement.For(
                invoiceMapping,
                null,
                JournalEntrySourceType.Invoice,
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate));
            requirements.Add(MappingRequirement.For(
                controlMapping,
                null,
                JournalEntrySourceType.Invoice,
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate));
            if (invoice.HasCost)
            {
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.Inventory,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.CostOfGoodsSold,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
            }
        }

        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue &&
                voucher.VoucherDate >= startDate &&
                voucher.VoucherDate <= endDate)
            .Select(voucher => new
            {
                voucher.Id,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.Direction,
                voucher.CashboxId,
                voucher.PartyType,
                voucher.CashMovementTypeId,
                voucher.AccountId
            })
            .ToListAsync(cancellationToken);
        foreach (var voucher in vouchers)
        {
            if (voucher.CashboxId.HasValue)
            {
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.Cashbox,
                    voucher.CashboxId,
                    JournalEntrySourceType.CashVoucher,
                    voucher.Id,
                    voucher.VoucherNumber,
                    voucher.VoucherDate));
            }

            if (voucher.AccountId.HasValue)
            {
                continue;
            }

            var mappingType = voucher.PartyType switch
            {
                CashPartyType.Partner when
                    voucher.Direction == CashDirection.Receipt =>
                    AccountingMappingType.CustomerControl,
                CashPartyType.Partner => AccountingMappingType.SupplierControl,
                CashPartyType.Driver => AccountingMappingType.DriverControl,
                CashPartyType.Employee => AccountingMappingType.EmployeeControl,
                _ => AccountingMappingType.CashMovementType
            };
            requirements.Add(MappingRequirement.For(
                mappingType,
                mappingType == AccountingMappingType.CashMovementType
                    ? voucher.CashMovementTypeId
                    : null,
                JournalEntrySourceType.CashVoucher,
                voucher.Id,
                voucher.VoucherNumber,
                voucher.VoucherDate));
        }

        var transferCashboxes = await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.TransferDate >= startDate &&
                transfer.TransferDate <= endDate)
            .Select(transfer => new
            {
                transfer.SourceCashboxId,
                transfer.DestinationCashboxId
            })
            .ToListAsync(cancellationToken);
        foreach (var cashboxId in transferCashboxes
                     .SelectMany(transfer => new[]
                     {
                         transfer.SourceCashboxId,
                         transfer.DestinationCashboxId
                     })
                     .Distinct())
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Cashbox,
                cashboxId));
        }

        await AddSimpleRequirementsAsync(
            requirements,
            startDate,
            endDate,
            cancellationToken);
        return requirements;
    }

    private async Task AddSimpleRequirementsAsync(
        List<MappingRequirement> requirements,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var hasStockAdjustments = await dbContext.StockAdjustments.AnyAsync(
            adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.DocumentDate >= startDate &&
                adjustment.DocumentDate <= endDate,
            cancellationToken);
        if (hasStockAdjustments)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Inventory,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.InventoryAdjustmentGain,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.InventoryAdjustmentLoss,
                null));
        }

        var hasStockOpenings = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate,
            cancellationToken);
        var hasOtherOpenings = await dbContext.PartnerOpeningBalances.AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken) ||
            await dbContext.EmployeeOpeningBalances.AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    !balance.PayrollEntryId.HasValue &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken) ||
            await dbContext.Cashboxes.AnyAsync(
                cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.BaseOpeningBalance != 0m &&
                    cashbox.OpeningBalanceDate >= startDate &&
                    cashbox.OpeningBalanceDate <= endDate,
                cancellationToken);
        if (hasStockOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Inventory,
                null));
        }

        if (hasStockOpenings || hasOtherOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.OpeningBalanceEquity,
                null));
        }

        var hasPartnerOpenings = await dbContext.PartnerOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate,
            cancellationToken);
        if (hasPartnerOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.CustomerControl,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.SupplierControl,
                null));
        }

        var hasEmployeeOpenings = await dbContext.EmployeeOpeningBalances
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    !balance.PayrollEntryId.HasValue &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken);
        if (hasEmployeeOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.EmployeeControl,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.EmployeeReceivable,
                null));
        }

        var cashboxOpeningIds = await dbContext.Cashboxes
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.BaseOpeningBalance != 0m &&
                cashbox.OpeningBalanceDate >= startDate &&
                cashbox.OpeningBalanceDate <= endDate)
            .Select(cashbox => cashbox.Id)
            .ToListAsync(cancellationToken);
        requirements.AddRange(cashboxOpeningIds
            .Select(cashboxId => MappingRequirement.For(
                AccountingMappingType.Cashbox,
                cashboxId))
            .ToArray());

        var hasTrips = await dbContext.DriverTrips.AnyAsync(
            trip =>
                trip.CompanyId == companyId &&
                trip.Cost.HasValue &&
                trip.Cost.Value > 0m &&
                trip.TripDate >= startDate &&
                trip.TripDate <= endDate,
            cancellationToken);
        if (hasTrips)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.DriverTripExpense,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.DriverControl,
                null));
        }
    }

    private async Task<Result<FiscalYearSnapshot>> GetFiscalYearAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        if (fiscalYearId <= 0)
        {
            return Result<FiscalYearSnapshot>.Failure(
                InvalidFiscalYearId());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.Id == fiscalYearId)
            .Select(year => new FiscalYearSnapshot(
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate,
                year.Status))
            .SingleOrDefaultAsync(cancellationToken);
        return fiscalYear is null
            ? Result<FiscalYearSnapshot>.Failure(
                FiscalYearNotFound(fiscalYearId))
            : Result<FiscalYearSnapshot>.Success(fiscalYear);
    }

    private static AccountingReadinessIssue CreateSourceIssue(
        string issueType,
        SourceDescriptor source,
        string message) =>
        new(
            issueType,
            source.Key.SourceType,
            source.Key.SourceId,
            source.SourceNumber,
            source.SourceDate,
            null,
            null,
            message);

    private static JournalEntrySourceType? MapMovementSourceType(
        ItemMovementType movementType) =>
        movementType switch
        {
            ItemMovementType.Sales or
            ItemMovementType.SalesReturn or
            ItemMovementType.Purchase or
            ItemMovementType.PurchaseReturn => JournalEntrySourceType.Invoice,
            ItemMovementType.OpeningBalance =>
                JournalEntrySourceType.StockOpeningBalance,
            ItemMovementType.AdjustmentIncrease or
            ItemMovementType.AdjustmentDecrease =>
                JournalEntrySourceType.StockAdjustment,
            _ => null
        };

    private static void RecordReadinessCheck(string result) =>
        ReadinessChecks.Add(
            1,
            new KeyValuePair<string, object?>("operation", "readiness"),
            new KeyValuePair<string, object?>("result", result));

    private static void RecordBackfill(string result) =>
        Backfills.Add(
            1,
            new KeyValuePair<string, object?>("operation", "backfill"),
            new KeyValuePair<string, object?>("result", result));

    private static void RecordFailure(string operation, string result) =>
        ReadinessFailures.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result));

    private static class LogEvents
    {
        public static readonly EventId ReadinessStarted =
            new(5201, "AccountingReadinessStarted");
        public static readonly EventId ReadinessCompleted =
            new(5202, "AccountingReadinessCompleted");
        public static readonly EventId ReadinessFailed =
            new(5203, "AccountingReadinessFailed");
        public static readonly EventId BackfillStarted =
            new(5211, "AccountingBackfillStarted");
        public static readonly EventId BackfillCompleted =
            new(5212, "AccountingBackfillCompleted");
        public static readonly EventId BackfillFailed =
            new(5213, "AccountingBackfillFailed");
    }

    private sealed record FiscalYearSnapshot(
        int Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        FiscalYearStatus Status);

    private sealed record SourceKey(
        JournalEntrySourceType SourceType,
        int SourceId);

    private sealed record SourceDescriptor(
        SourceKey Key,
        string SourceNumber,
        DateOnly SourceDate);

    private sealed record MappingRequirement(
        AccountingMappingType MappingType,
        int? SourceId,
        JournalEntrySourceType? SourceType,
        int? SourceDocumentId,
        string? SourceNumber,
        DateOnly? SourceDate)
    {
        public static MappingRequirement For(
            AccountingMappingType mappingType,
            int? sourceId,
            JournalEntrySourceType? sourceType = null,
            int? sourceDocumentId = null,
            string? sourceNumber = null,
            DateOnly? sourceDate = null) =>
            new(
                mappingType,
                sourceId,
                sourceType,
                sourceDocumentId,
                sourceNumber,
                sourceDate);
    }
}
