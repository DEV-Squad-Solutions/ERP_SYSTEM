using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed class JournalEntryService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
    : IJournalEntryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

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
        var search = filters.Search?.Trim();
        var query = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.CompanyId == companyId)
            .Where(entry =>
                string.IsNullOrEmpty(search) ||
                entry.EntryNumber.Contains(search) ||
                entry.Description.Contains(search))
            .Where(entry =>
                !filters.FiscalYearId.HasValue ||
                entry.FiscalYearId == filters.FiscalYearId.Value)
            .Where(entry =>
                !filters.EntryType.HasValue ||
                entry.EntryType == filters.EntryType.Value)
            .Where(entry =>
                !filters.Status.HasValue ||
                entry.Status == filters.Status.Value)
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
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<JournalEntryResponse>.Failure(InvalidId());
        }

        var response = (await LoadResponsesAsync([id], cancellationToken))
            .SingleOrDefault();
        return response is null
            ? Result<JournalEntryResponse>.Failure(NotFound(id))
            : Result<JournalEntryResponse>.Success(response);
    }

    public async Task<Result<JournalEntryResponse>> AddAsync(
        JournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var balanceValidation = ValidateBalance(request.Lines);
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

        var accountValidation = await ValidateAccountsAsync(
            request.Lines,
            request.FiscalYearId,
            cancellationToken);
        if (accountValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                accountValidation.Errors);
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
            Lines = request.Lines.Select(line => new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = line.AccountId,
                Description = NormalizeOptional(line.Description),
                Debit = line.Debit,
                Credit = line.Credit
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

    public async Task<Result<JournalEntryResponse>> ReverseAsync(
        int id,
        JournalEntryReverseRequest request,
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

        if (entry.Status == JournalEntryStatus.Reversed)
        {
            return Result<JournalEntryResponse>.Failure(AlreadyReversed());
        }

        if (entry.ReversalOfEntryId.HasValue)
        {
            return Result<JournalEntryResponse>.Failure(CannotReverseReversal());
        }

        if (request.ReversalDate < entry.EntryDate)
        {
            return Result<JournalEntryResponse>.Failure(
                ReversalDateBeforeEntry());
        }

        if (entry.FiscalYear.Status == FiscalYearStatus.Closed)
        {
            return Result<JournalEntryResponse>.Failure(FiscalYearClosed());
        }

        if (request.ReversalDate < entry.FiscalYear.StartDate ||
            request.ReversalDate > entry.FiscalYear.EndDate)
        {
            return Result<JournalEntryResponse>.Failure(
                EntryDateOutsideFiscalYear());
        }

        var reversalNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "JV",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(journalEntry => journalEntry.CompanyId == companyId)
                .Select(journalEntry => journalEntry.EntryNumber),
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reversal = new JournalEntry
        {
            CompanyId = companyId,
            FiscalYearId = entry.FiscalYearId,
            EntryNumber = reversalNumber,
            EntryDate = request.ReversalDate,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"عكس القيد {entry.EntryNumber}: {entry.Description}"
                : request.Description.Trim(),
            EntryType = entry.EntryType,
            Status = JournalEntryStatus.Posted,
            PostedOn = now,
            ReversalOfEntryId = entry.Id,
            Lines = entry.Lines
                .OrderBy(line => line.Id)
                .Select(line => new JournalEntryLine
                {
                    CompanyId = companyId,
                    AccountId = line.AccountId,
                    Description = line.Description,
                    Debit = line.Credit,
                    Credit = line.Debit
                })
                .ToList()
        };

        entry.Status = JournalEntryStatus.Reversed;
        entry.ReversedOn = now;
        dbContext.Entry(entry)
            .Property(journalEntry => journalEntry.RowVersion)
            .OriginalValue = request.RowVersion;
        dbContext.JournalEntries.Add(reversal);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<JournalEntryResponse>.Failure(Concurrency());
        }

        var response = (await LoadResponsesAsync(
            [reversal.Id],
            cancellationToken)).Single();
        return Result<JournalEntryResponse>.Success(response);
    }

    private async Task<Result> ValidateFiscalYearAsync(
        int fiscalYearId,
        DateOnly entryDate,
        CancellationToken cancellationToken)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.Id == fiscalYearId)
            .Select(year => new
            {
                year.StartDate,
                year.EndDate,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(FiscalYearNotFound(fiscalYearId));
        }

        if (entryDate < fiscalYear.StartDate || entryDate > fiscalYear.EndDate)
        {
            return Result.Failure(EntryDateOutsideFiscalYear());
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result.Success()
            : Result.Failure(FiscalYearClosed());
    }

    private async Task<Result> ValidateAccountsAsync(
        IReadOnlyList<JournalEntryLineRequest> lines,
        int fiscalYearId,
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
        var mappedAccountIds = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                accountIds.Contains(mapping.AccountId))
            .Select(mapping => mapping.AccountId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
        var errors = new List<Error>();
        for (var index = 0; index < lines.Count; index++)
        {
            var accountId = lines[index].AccountId;
            if (!accounts.TryGetValue(accountId, out var account))
            {
                errors.Add(AccountNotFound(accountId, index));
            }
            else if (!account.IsActive)
            {
                errors.Add(AccountInactive(accountId, index));
            }
            else if (!account.IsPosting)
            {
                errors.Add(AccountNotPosting(accountId, index));
            }
            else if (!account.ParentAccountId.HasValue)
            {
                errors.Add(AccountMustBeChild(accountId, index));
            }
            else if (mappedAccountIds.Contains(accountId))
            {
                errors.Add(AccountLinkedToOperationalData(accountId, index));
            }
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors);
    }

    private static Result ValidateBalance(
        IReadOnlyList<JournalEntryLineRequest> lines)
    {
        var totalDebit = lines.Sum(line => line.Debit);
        var totalCredit = lines.Sum(line => line.Credit);
        return totalDebit > 0m && totalDebit == totalCredit
            ? Result.Success()
            : Result.Failure(Unbalanced());
    }

    private async Task<IReadOnlyList<JournalEntryResponse>> LoadResponsesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var entries = await dbContext.JournalEntries
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entry => entry.FiscalYear)
            .Include(entry => entry.Lines)
                .ThenInclude(line => line.Account)
            .Where(entry =>
                entry.CompanyId == companyId &&
                ids.Contains(entry.Id))
            .ToListAsync(cancellationToken);

        var relatedIds = entries
            .Where(entry => entry.ReversalOfEntryId.HasValue)
            .Select(entry => entry.ReversalOfEntryId!.Value)
            .Distinct()
            .ToArray();
        var relatedNumbers = relatedIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.JournalEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.CompanyId == companyId &&
                    relatedIds.Contains(entry.Id))
                .ToDictionaryAsync(
                    entry => entry.Id,
                    entry => entry.EntryNumber,
                    cancellationToken);

        var reversals = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.ReversalOfEntryId.HasValue &&
                ids.Contains(entry.ReversalOfEntryId.Value))
            .Select(entry => new
            {
                OriginalId = entry.ReversalOfEntryId!.Value,
                entry.Id,
                entry.EntryNumber
            })
            .ToDictionaryAsync(entry => entry.OriginalId, cancellationToken);

        return entries
            .Select(entry =>
            {
                reversals.TryGetValue(entry.Id, out var reversedBy);
                var lines = entry.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new JournalEntryLineResponse(
                        Id: line.Id,
                        AccountId: line.AccountId,
                        AccountCode: line.Account.Code,
                        AccountName: line.Account.Name,
                        Description: line.Description,
                        Debit: line.Debit,
                        Credit: line.Credit))
                    .ToArray();
                return new JournalEntryResponse(
                    Id: entry.Id,
                    CompanyId: entry.CompanyId,
                    FiscalYearId: entry.FiscalYearId,
                    FiscalYearName: entry.FiscalYear.Name,
                    EntryNumber: entry.EntryNumber,
                    EntryDate: entry.EntryDate,
                    Description: entry.Description,
                    EntryType: entry.EntryType,
                    Status: entry.Status,
                    TotalDebit: lines.Sum(line => line.Debit),
                    TotalCredit: lines.Sum(line => line.Credit),
                    PostedOn: entry.PostedOn,
                    ReversedOn: entry.ReversedOn,
                    ReversalOfEntryId: entry.ReversalOfEntryId,
                    ReversalOfEntryNumber: entry.ReversalOfEntryId.HasValue &&
                        relatedNumbers.TryGetValue(
                            entry.ReversalOfEntryId.Value,
                            out var originalNumber)
                            ? originalNumber
                            : null,
                    ReversedByEntryId: reversedBy?.Id,
                    ReversedByEntryNumber: reversedBy?.EntryNumber,
                    CreatedById: entry.CreatedById,
                    CreatedOn: entry.CreatedOn,
                    RowVersion: entry.RowVersion,
                    Lines: lines);
            })
            .ToArray();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
