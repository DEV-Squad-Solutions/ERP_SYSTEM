using System.Data;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed class AutomaticPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    ILogger<AutomaticPostingService> logger)
    : IAutomaticPostingService, IScopedService
{
    private const string MeterName = "MiniErp.Accounting";
    private static readonly Meter AccountingMeter = new(MeterName);
    private static readonly Counter<long> AutomaticPostingOperations =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.automatic_posting.operations",
            unit: "{operation}",
            description: "Automatic journal posting outcomes.");
    private readonly int companyId = currentCompanyContext.CompanyId;

    public Task<Result<AutomaticJournalEntryResult>> CreateOrGetAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken = default) =>
        SaveAsync(request, updateExisting: false, cancellationToken);

    public Task<Result<AutomaticJournalEntryResult>> CreateOrUpdateAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken = default) =>
        SaveAsync(request, updateExisting: true, cancellationToken);

    public async Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(sourceType) || sourceId <= 0)
        {
            LogFailure(
                operation: "Delete",
                sourceType,
                sourceId,
                fiscalYearId: null,
                failureKind: "InvalidSource");
            return Result.Failure(AutomaticSourceRequired());
        }

        var ownedTransaction = await BeginOwnedTransactionAsync(
            cancellationToken);
        try
        {
            var entry = await ActiveSourceQuery(sourceType, sourceId)
                .Include(journalEntry => journalEntry.Lines)
                .SingleOrDefaultAsync(cancellationToken);
            if (entry is null)
            {
                await CommitOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                logger.LogInformation(
                    LogEvents.DeleteIdempotent,
                    "Automatic posting delete was idempotent for company {CompanyId}, source type {SourceType}, source id {SourceId}",
                    companyId,
                    sourceType,
                    sourceId);
                RecordOperation("delete", "idempotent", sourceType);
                return Result.Success();
            }

            var fiscalYearIsOpen = await dbContext.FiscalYears
                .AsNoTracking()
                .AnyAsync(year =>
                    year.CompanyId == companyId &&
                    year.Id == entry.FiscalYearId &&
                    year.Status == FiscalYearStatus.Open,
                    cancellationToken);
            if (!fiscalYearIsOpen)
            {
                LogFailure(
                    operation: "Delete",
                    sourceType,
                    sourceId,
                    entry.FiscalYearId,
                    failureKind: "FiscalYearClosed");
                return Result.Failure(FiscalYearClosed());
            }

            dbContext.JournalEntryLines.RemoveRange(entry.Lines);
            dbContext.JournalEntries.Remove(entry);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await CommitOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                logger.LogInformation(
                    LogEvents.Deleted,
                    "Automatic journal deleted for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, journal entry id {JournalEntryId}",
                    companyId,
                    entry.FiscalYearId,
                    sourceType,
                    sourceId,
                    entry.Id);
                RecordOperation("delete", "deleted", sourceType);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                if (ownedTransaction is not null)
                {
                    dbContext.ChangeTracker.Clear();
                }

                logger.LogWarning(
                    LogEvents.Failed,
                    exception,
                    "Automatic posting {Operation} failed for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}",
                    "Delete",
                    companyId,
                    entry.FiscalYearId,
                    sourceType,
                    sourceId,
                    "Concurrency");
                RecordOperation("delete", "failed", sourceType);
                return Result.Failure(Concurrency());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RollbackOwnedTransactionAsync(
                ownedTransaction,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await RollbackOwnedTransactionAsync(
                ownedTransaction,
                cancellationToken);
            logger.LogError(
                LogEvents.Failed,
                exception,
                "Automatic posting {Operation} failed for company {CompanyId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}",
                "Delete",
                companyId,
                sourceType,
                sourceId,
                "Unexpected");
            RecordOperation("delete", "failed", sourceType);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private async Task<Result<AutomaticJournalEntryResult>> SaveAsync(
        AutomaticJournalEntryRequest request,
        bool updateExisting,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequestAsync(request, cancellationToken);
        if (validation.IsFailure)
        {
            LogFailure(
                updateExisting ? "CreateOrUpdate" : "CreateOrGet",
                request.SourceType,
                request.SourceId,
                request.FiscalYearId,
                failureKind: "Validation",
                validation.Errors.Count);
            return Result<AutomaticJournalEntryResult>.Failure(
                validation.Errors);
        }

        var ownedTransaction = await BeginOwnedTransactionAsync(
            cancellationToken);
        try
        {
            var existing = await ActiveSourceQuery(
                    request.SourceType,
                    request.SourceId)
                .Include(entry => entry.Lines)
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null && !updateExisting)
            {
                await CommitOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                logger.LogInformation(
                    LogEvents.Idempotent,
                    "Automatic posting was idempotent for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, journal entry id {JournalEntryId}",
                    companyId,
                    existing.FiscalYearId,
                    request.SourceType,
                    request.SourceId,
                    existing.Id);
                RecordOperation("create_or_get", "idempotent", request.SourceType);
                return Result<AutomaticJournalEntryResult>.Success(
                    ToResult(existing, created: false));
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var created = existing is null;
            JournalEntry entry;
            if (created)
            {
                entry = BuildEntry(
                    request,
                    await GenerateEntryNumberAsync(cancellationToken),
                    now);
                dbContext.JournalEntries.Add(entry);
            }
            else
            {
                var oldFiscalYearIsOpen = await dbContext.FiscalYears
                    .AsNoTracking()
                    .AnyAsync(year =>
                        year.CompanyId == companyId &&
                        year.Id == existing!.FiscalYearId &&
                        year.Status == FiscalYearStatus.Open,
                        cancellationToken);
                if (!oldFiscalYearIsOpen)
                {
                    LogFailure(
                        operation: "CreateOrUpdate",
                        request.SourceType,
                        request.SourceId,
                        existing?.FiscalYearId,
                        failureKind: "FiscalYearClosed");
                    return Result<AutomaticJournalEntryResult>.Failure(
                        FiscalYearClosed());
                }

                entry = existing!;
                dbContext.JournalEntryLines.RemoveRange(entry.Lines);
                entry.FiscalYearId = request.FiscalYearId;
                entry.EntryDate = request.EntryDate;
                entry.Description = request.Description.Trim();
                entry.SourceNumber = NormalizeOptional(request.SourceNumber);
                entry.PostedOn = now;
                entry.Lines = BuildLines(request.Lines);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await CommitOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                logger.LogInformation(
                    created ? LogEvents.Created : LogEvents.Updated,
                    "Automatic journal {PostingOutcome} for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, journal entry id {JournalEntryId}, line count {LineCount}",
                    created ? "created" : "updated",
                    companyId,
                    entry.FiscalYearId,
                    request.SourceType,
                    request.SourceId,
                    entry.Id,
                    entry.Lines.Count);
                RecordOperation(
                    updateExisting ? "create_or_update" : "create_or_get",
                    created ? "created" : "updated",
                    request.SourceType);
                return Result<AutomaticJournalEntryResult>.Success(
                    ToResult(entry, created));
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                if (ownedTransaction is not null)
                {
                    dbContext.ChangeTracker.Clear();
                }

                logger.LogWarning(
                    LogEvents.Failed,
                    exception,
                    "Automatic posting {Operation} failed for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}",
                    updateExisting ? "CreateOrUpdate" : "CreateOrGet",
                    companyId,
                    request.FiscalYearId,
                    request.SourceType,
                    request.SourceId,
                    "Concurrency");
                RecordOperation(
                    updateExisting ? "create_or_update" : "create_or_get",
                    "failed",
                    request.SourceType);
                return Result<AutomaticJournalEntryResult>.Failure(
                    Concurrency());
            }
            catch (DbUpdateException exception)
            {
                await RollbackOwnedTransactionAsync(
                    ownedTransaction,
                    cancellationToken);
                if (ownedTransaction is not null)
                {
                    dbContext.ChangeTracker.Clear();
                }

                logger.LogError(
                    LogEvents.Failed,
                    exception,
                    "Automatic posting {Operation} failed for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}",
                    updateExisting ? "CreateOrUpdate" : "CreateOrGet",
                    companyId,
                    request.FiscalYearId,
                    request.SourceType,
                    request.SourceId,
                    "DatabaseUpdate");
                RecordOperation(
                    updateExisting ? "create_or_update" : "create_or_get",
                    "failed",
                    request.SourceType);
                return Result<AutomaticJournalEntryResult>.Failure(
                    AutomaticDuplicate());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RollbackOwnedTransactionAsync(
                ownedTransaction,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await RollbackOwnedTransactionAsync(
                ownedTransaction,
                cancellationToken);
            logger.LogError(
                LogEvents.Failed,
                exception,
                "Automatic posting {Operation} failed for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}",
                updateExisting ? "CreateOrUpdate" : "CreateOrGet",
                companyId,
                request.FiscalYearId,
                request.SourceType,
                request.SourceId,
                "Unexpected");
            RecordOperation(
                updateExisting ? "create_or_update" : "create_or_get",
                "failed",
                request.SourceType);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private async Task<Result> ValidateRequestAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.SourceType) || request.SourceId <= 0)
        {
            return Result.Failure(AutomaticSourceRequired());
        }

        if (ValidateBalance(request.Lines) is { } balanceError)
        {
            return Result.Failure(balanceError);
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.Id == request.FiscalYearId)
            .Select(year => new
            {
                year.StartDate,
                year.EndDate,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(FiscalYearNotFound(request.FiscalYearId));
        }

        if (request.EntryDate < fiscalYear.StartDate ||
            request.EntryDate > fiscalYear.EndDate)
        {
            return Result.Failure(EntryDateOutsideFiscalYear());
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result.Failure(FiscalYearClosed());
        }

        return await ValidateAccountsAsync(request.Lines, cancellationToken)
            is { } accountError
            ? Result.Failure(accountError)
            : Result.Success();
    }

    private async Task<IDbContextTransaction?> BeginOwnedTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
    }

    private static Task CommitOwnedTransactionAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken) =>
        transaction is null
            ? Task.CompletedTask
            : transaction.CommitAsync(cancellationToken);

    private static Task RollbackOwnedTransactionAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken) =>
        transaction is null
            ? Task.CompletedTask
            : transaction.RollbackAsync(cancellationToken);

    private IQueryable<JournalEntry> ActiveSourceQuery(
        JournalEntrySourceType sourceType,
        int sourceId) =>
        dbContext.JournalEntries.Where(entry =>
            entry.CompanyId == companyId &&
            entry.EntryType == JournalEntryType.Automatic &&
            entry.Status == JournalEntryStatus.Posted &&
            entry.ReversalOfEntryId == null &&
            entry.SourceType == sourceType &&
            entry.SourceId == sourceId);

    private async Task<string> GenerateEntryNumberAsync(
        CancellationToken cancellationToken) =>
        await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "JV",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.CompanyId == companyId)
                .Select(entry => entry.EntryNumber),
            cancellationToken);

    private JournalEntry BuildEntry(
        AutomaticJournalEntryRequest request,
        string entryNumber,
        DateTime now) =>
        new()
        {
            CompanyId = companyId,
            FiscalYearId = request.FiscalYearId,
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            EntryType = JournalEntryType.Automatic,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            SourceNumber = NormalizeOptional(request.SourceNumber),
            Status = JournalEntryStatus.Posted,
            PostedOn = now,
            Lines = BuildLines(request.Lines)
        };

    private List<JournalEntryLine> BuildLines(
        IReadOnlyList<JournalEntryLineRequest> lines) =>
        lines
            .Where(line => line.Debit > 0m || line.Credit > 0m)
            .Select(line => new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = line.AccountId,
                Description = NormalizeOptional(line.Description),
                Debit = line.Debit,
                Credit = line.Credit
            })
            .ToList();

    private async Task<Error?> ValidateAccountsAsync(
        IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var accountIds = lines
            .Select(line => line.AccountId)
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
                account.IsActive,
                account.IsPosting,
                account.ParentAccountId
            })
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        for (var index = 0; index < lines.Count; index++)
        {
            var accountId = lines[index].AccountId;
            if (!accounts.TryGetValue(accountId, out var account))
            {
                return AccountNotFound(accountId, index);
            }

            if (!account.IsActive)
            {
                return AccountInactive(accountId, index);
            }

            if (!account.IsPosting)
            {
                return AccountNotPosting(accountId, index);
            }

            if (!account.ParentAccountId.HasValue)
            {
                return AccountMustBeChild(accountId, index);
            }
        }

        return null;
    }

    private static AutomaticJournalEntryResult ToResult(
        JournalEntry entry,
        bool created) =>
        new(
            JournalEntryId: entry.Id,
            EntryNumber: entry.EntryNumber,
            Created: created);

    private static Error? ValidateBalance(
        IReadOnlyList<JournalEntryLineRequest>? lines)
    {
        if (lines is null)
        {
            return Unbalanced();
        }

        var effectiveLines = lines
            .Where(line => line.Debit != 0m || line.Credit != 0m)
            .ToArray();
        if (effectiveLines.Length < 2 || effectiveLines.Any(line =>
                line.Debit < 0m ||
                line.Credit < 0m ||
                (line.Debit > 0m) == (line.Credit > 0m)))
        {
            return Unbalanced();
        }

        var totalDebit = effectiveLines.Sum(line => line.Debit);
        var totalCredit = effectiveLines.Sum(line => line.Credit);
        return totalDebit > 0m && totalDebit == totalCredit
            ? null
            : Unbalanced();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void LogFailure(
        string operation,
        JournalEntrySourceType sourceType,
        int sourceId,
        int? fiscalYearId,
        string failureKind,
        int errorCount = 1)
    {
        logger.LogWarning(
            LogEvents.Failed,
            "Automatic posting {Operation} failed for company {CompanyId}, fiscal year {FiscalYearId}, source type {SourceType}, source id {SourceId}, failure kind {FailureKind}, error count {ErrorCount}",
            operation,
            companyId,
            fiscalYearId,
            sourceType,
            sourceId,
            failureKind,
            errorCount);

        RecordOperation(
            operation switch
            {
                "CreateOrGet" => "create_or_get",
                "CreateOrUpdate" => "create_or_update",
                _ => "delete"
            },
            "failed",
            sourceType);
    }

    private static void RecordOperation(
        string operation,
        string result,
        JournalEntrySourceType sourceType) =>
        AutomaticPostingOperations.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>(
                "source_type",
                Enum.IsDefined(sourceType) ? sourceType.ToString() : "invalid"));

    private static class LogEvents
    {
        public static readonly EventId Created = new(5101, "AutomaticJournalCreated");
        public static readonly EventId Updated = new(5102, "AutomaticJournalUpdated");
        public static readonly EventId Idempotent = new(5103, "AutomaticPostingIdempotent");
        public static readonly EventId Deleted = new(5104, "AutomaticJournalDeleted");
        public static readonly EventId DeleteIdempotent = new(5105, "AutomaticPostingDeleteIdempotent");
        public static readonly EventId Failed = new(5106, "AutomaticPostingFailed");
    }
}
