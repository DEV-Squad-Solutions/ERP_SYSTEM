using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.CashboxRevaluations;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed partial class JournalEntryService
{
    public async Task<Result<PagedResponse<JournalEntryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        JournalEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<JournalEntryResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        filters ??= new JournalEntryFilterRequest();
        var validation = new JournalEntryFilterRequestValidator().Validate(filters);
        if (!validation.IsValid)
            return Result<PagedResponse<JournalEntryResponse>>.Failure(
                validation.Errors.Select(error => Error.Validation(
                    "JournalEntries.FilterValidation", error.ErrorMessage, error.PropertyName)));
        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<JournalEntryResponse>>.Failure(
                fiscalYear.Errors);
        }

        var search = filters.Search?.Trim();
        var scope = cashboxAccessService is null ? null
            : await cashboxAccessService.GetScopeAsync(cancellationToken);
        if (scope?.IsFailure == true)
            return Result<PagedResponse<JournalEntryResponse>>.Failure(scope.Errors);
        var allCashboxes = scope is null || scope.Value.HasAllCashboxes;
        var visibleIds = scope?.Value.ViewCashboxIds ?? [];
        var query = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.FiscalYearId == fiscalYear.Value.FiscalYearId);
        if (!allCashboxes)
            query = query.Where(entry => !entry.Lines.Any(line =>
                (line.PartyType == JournalPartyType.Cashbox &&
                    (!line.PartyId.HasValue || !visibleIds.Contains(line.PartyId.Value))) ||
                (line.PartyType != JournalPartyType.Cashbox && dbContext.AccountMappings.Any(mapping =>
                    mapping.CompanyId == companyId && mapping.FiscalYearId == entry.FiscalYearId &&
                    mapping.MappingType == AccountingMappingType.Cashbox && mapping.AccountId == line.AccountId))));

        query = query
            .Where(entry =>
                string.IsNullOrEmpty(search) ||
                entry.EntryNumber.Contains(search) ||
                entry.Description.Contains(search))
            .Where(entry =>
                !filters.EntryType.HasValue ||
                entry.EntryType == filters.EntryType.Value)
            .Where(entry =>
                !filters.Status.HasValue ||
                entry.Status == filters.Status.Value)
            .Where(entry =>
                !filters.SourceType.HasValue ||
                (entry.SourceType == filters.SourceType.Value &&
                 entry.SourceId == filters.SourceId))
            .Where(entry =>
                !filters.FromDate.HasValue ||
                entry.EntryDate >= filters.FromDate.Value)
            .Where(entry =>
                !filters.ToDate.HasValue ||
                entry.EntryDate <= filters.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        IReadOnlyList<JournalEntryResponse> items = [];
        if (offset < totalCount)
        {
            var ids = await query
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.Id)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(entry => entry.Id)
                .ToArrayAsync(cancellationToken);

            items = (await LoadResponsesAsync(ids, cancellationToken))
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.Id)
                .ToArray();
        }

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);
        return Result<PagedResponse<JournalEntryResponse>>.Success(
            new PagedResponse<JournalEntryResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    public async Task<Result<JournalEntryResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<JournalEntryResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(fiscalYear.Errors);
        }

        var access = await EnsureEntryCashboxesAsync(id, execute: false, cancellationToken);
        if (access.IsFailure) return Result<JournalEntryResponse>.Failure(access.Errors);

        var response = (await LoadResponsesAsync(
                [id],
                cancellationToken,
                fiscalYear.Value.FiscalYearId))
            .SingleOrDefault();
        return response is null
            ? Result<JournalEntryResponse>.Failure(NotFound(id))
            : Result<JournalEntryResponse>.Success(response);
    }

    public async Task<Result<JournalEntryResponse>> AddAsync(
        JournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EntryType == JournalEntryType.Automatic)
        {
            return Result<JournalEntryResponse>.Failure(
                AutomaticCannotBeCreatedManually());
        }

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        var normalizedResult = JournalEntryLineNormalizer.Normalize(
            request.Lines,
            baseCurrency);
        if (normalizedResult.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(normalizedResult.Errors);
        }

        var lines = normalizedResult.Value;
        var balanceValidation = ValidateBalance(lines);
        if (balanceValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                balanceValidation.Errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            request.FiscalYearId,
            request.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                fiscalYearValidation.Errors);
        }

        if (request.EntryType == JournalEntryType.Opening &&
            await HasDriverOpeningConflictAsync(request.FiscalYearId, request.EntryDate, lines, cancellationToken))
        {
            return Result<JournalEntryResponse>.Failure(DriverOpeningBalanceConflict());
        }

        var accountValidation = await ValidateAccountsAsync(
            lines,
            request.FiscalYearId,
            cancellationToken);
        if (accountValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                accountValidation.Errors);
        }

        var revaluationGuardError = await CashboxRevaluationGuard.ValidateAsync(
            dbContext,
            companyId,
            request.EntryDate,
            lines.Where(line => line.PartyType == JournalPartyType.Cashbox)
                .Select(line => line.PartyId ?? 0),
            cancellationToken);
        if (revaluationGuardError is not null)
        {
            return Result<JournalEntryResponse>.Failure(revaluationGuardError);
        }

        var entryNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "JV",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.CompanyId == companyId)
                .Select(entry => entry.EntryNumber),
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entry = new JournalEntry
        {
            CompanyId = companyId,
            FiscalYearId = request.FiscalYearId,
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            EntryType = request.EntryType,
            Status = JournalEntryStatus.Posted,
            PostedOn = now,
            Lines = lines.Select(line => new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = line.AccountId,
                PartyType = line.PartyType,
                PartyId = line.PartyId,
                Description = NormalizeOptional(line.Description),
                Debit = line.Debit,
                Credit = line.Credit,
                Currency = line.Currency!.Value,
                ExchangeRate = line.ExchangeRate!.Value,
                TransactionDebit = line.TransactionDebit!.Value,
                TransactionCredit = line.TransactionCredit!.Value
            }).ToList()
        };

        dbContext.JournalEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = (await LoadResponsesAsync(
            [entry.Id],
            cancellationToken)).Single();
        return Result<JournalEntryResponse>.Success(response);
    }

    public async Task<Result<JournalEntryResponse>> UpdateAsync(
        int id,
        JournalEntryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<JournalEntryResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<JournalEntryResponse>.Failure(RowVersionRequired());
        }

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        var normalizedResult = JournalEntryLineNormalizer.Normalize(
            request.Lines,
            baseCurrency);
        if (normalizedResult.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(normalizedResult.Errors);
        }

        var lines = normalizedResult.Value;
        var balanceValidation = ValidateBalance(lines);
        if (balanceValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                balanceValidation.Errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var entry = await dbContext.JournalEntries
            .Include(journalEntry => journalEntry.FiscalYear)
            .Include(journalEntry => journalEntry.Lines)
            .FirstOrDefaultAsync(
                journalEntry =>
                    journalEntry.CompanyId == companyId &&
                    journalEntry.Id == id,
                cancellationToken);
        if (entry is null)
        {
            return Result<JournalEntryResponse>.Failure(NotFound(id));
        }

        if (!entry.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<JournalEntryResponse>.Failure(Concurrency());
        }
        var oldAccess = await EnsureEntryCashboxesAsync(id, execute: true, cancellationToken);
        if (oldAccess.IsFailure) return Result<JournalEntryResponse>.Failure(oldAccess.Errors);

        if (entry.EntryType == JournalEntryType.Automatic)
        {
            return Result<JournalEntryResponse>.Failure(AutomaticReadOnly());
        }

        if (entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
            (entry.Lines.Any(line => line.PartyType == JournalPartyType.Driver) ||
             lines.Any(line => line.PartyType == JournalPartyType.Driver)))
        {
            return Result<JournalEntryResponse>.Failure(DriverCarryForwardReadOnly());
        }

        if (entry.Status == JournalEntryStatus.Reversed ||
            entry.ReversalOfEntryId.HasValue)
        {
            return Result<JournalEntryResponse>.Failure(ReversedReadOnly());
        }

        if (entry.FiscalYearId != request.FiscalYearId)
        {
            return Result<JournalEntryResponse>.Failure(
                FiscalYearCannotBeChanged());
        }

        var affectedCashboxIds = entry.Lines
            .Where(line => line.PartyType == JournalPartyType.Cashbox)
            .Select(line => line.PartyId ?? 0)
            .Concat(lines.Where(line => line.PartyType == JournalPartyType.Cashbox)
                .Select(line => line.PartyId ?? 0));
        var revaluationGuardError = await CashboxRevaluationGuard.ValidateAsync(
            dbContext,
            companyId,
            entry.EntryDate <= request.EntryDate
                ? entry.EntryDate
                : request.EntryDate,
            affectedCashboxIds,
            cancellationToken);
        if (revaluationGuardError is not null)
        {
            return Result<JournalEntryResponse>.Failure(revaluationGuardError);
        }

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            request.FiscalYearId,
            request.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                fiscalYearValidation.Errors);
        }

        if (entry.FiscalYearId != request.FiscalYearId ||
            entry.EntryDate != request.EntryDate ||
            entry.FiscalYear.Status != FiscalYearStatus.Open)
        {
            var oldFiscalYearValidation = await ValidateFiscalYearAsync(
                entry.FiscalYearId,
                entry.EntryDate,
                cancellationToken);
            if (oldFiscalYearValidation.IsFailure)
            {
                return Result<JournalEntryResponse>.Failure(
                    oldFiscalYearValidation.Errors);
            }
        }

        var accountValidation = await ValidateAccountsAsync(
            lines,
            request.FiscalYearId,
            cancellationToken);
        if (accountValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                accountValidation.Errors);
        }

        if (entry.EntryType == JournalEntryType.Opening && entry.SourceType == null &&
            await HasDriverOpeningConflictAsync(entry.FiscalYearId, request.EntryDate, lines, cancellationToken))
        {
            return Result<JournalEntryResponse>.Failure(DriverOpeningBalanceConflict());
        }

        var entryState = dbContext.Entry(entry);
        entryState.Property(journalEntry => journalEntry.RowVersion)
            .OriginalValue = request.RowVersion;
        entry.FiscalYearId = request.FiscalYearId;
        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description.Trim();

        dbContext.JournalEntryLines.RemoveRange(entry.Lines);
        entry.Lines = lines.Select(line => new JournalEntryLine
        {
            CompanyId = companyId,
            AccountId = line.AccountId,
            PartyType = line.PartyType,
            PartyId = line.PartyId,
            Description = NormalizeOptional(line.Description),
            Debit = line.Debit,
            Credit = line.Credit,
            Currency = line.Currency!.Value,
            ExchangeRate = line.ExchangeRate!.Value,
            TransactionDebit = line.TransactionDebit!.Value,
            TransactionCredit = line.TransactionCredit!.Value
        }).ToList();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var response = (await LoadResponsesAsync([id], cancellationToken))
                .Single();
            await transaction.CommitAsync(cancellationToken);
            return Result<JournalEntryResponse>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<JournalEntryResponse>.Failure(Concurrency());
        }
    }

    public async Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        if (rowVersion is not { Length: 8 })
        {
            return Result.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var entry = await dbContext.JournalEntries
            .Include(journalEntry => journalEntry.FiscalYear)
            .Include(journalEntry => journalEntry.Lines)
            .FirstOrDefaultAsync(
                journalEntry =>
                    journalEntry.CompanyId == companyId &&
                    journalEntry.Id == id,
                cancellationToken);
        if (entry is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (!entry.RowVersion.SequenceEqual(rowVersion))
        {
            return Result.Failure(Concurrency());
        }
        var access = await EnsureEntryCashboxesAsync(id, execute: true, cancellationToken);
        if (access.IsFailure) return access;

        if (entry.EntryType == JournalEntryType.Automatic)
        {
            return Result.Failure(AutomaticReadOnly());
        }

        if (entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
            entry.Lines.Any(line => line.PartyType == JournalPartyType.Driver))
        {
            return Result.Failure(DriverCarryForwardReadOnly());
        }

        if (entry.Status == JournalEntryStatus.Reversed ||
            entry.ReversalOfEntryId.HasValue)
        {
            return Result.Failure(ReversedReadOnly());
        }

        var revaluationGuardError = await CashboxRevaluationGuard.ValidateAsync(
            dbContext,
            companyId,
            entry.EntryDate,
            entry.Lines.Where(line => line.PartyType == JournalPartyType.Cashbox)
                .Select(line => line.PartyId ?? 0),
            cancellationToken);
        if (revaluationGuardError is not null)
        {
            return Result.Failure(revaluationGuardError);
        }

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            entry.FiscalYearId,
            entry.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result.Failure(fiscalYearValidation.Errors);
        }

        dbContext.JournalEntryLines.RemoveRange(entry.Lines);
        dbContext.JournalEntries.Remove(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }
    }
}
