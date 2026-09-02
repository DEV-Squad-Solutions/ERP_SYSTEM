using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IAccountingReadinessService? accountingReadinessService = null)
    : IFiscalYearService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<FiscalYearResponse>>> GetAllAsync(
        PaginationRequest pagination,
        FiscalYearFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new FiscalYearFilterRequest();
        var search = filters.Search?.Trim();

        var query = dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear => fiscalYear.CompanyId == companyId)
            .Where(fiscalYear =>
                string.IsNullOrEmpty(search) ||
                fiscalYear.Name.Contains(search))
            .Where(fiscalYear =>
                !filters.Status.HasValue ||
                fiscalYear.Status == filters.Status.Value)
            .Where(fiscalYear =>
                !filters.IsCurrent.HasValue ||
                fiscalYear.IsCurrent == filters.IsCurrent.Value)
            .OrderByDescending(fiscalYear => fiscalYear.StartDate)
            .ThenByDescending(fiscalYear => fiscalYear.Id);

        return await paginationService.PaginateAsync<
            FiscalYear,
            FiscalYearResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<FiscalYearSelectResponse>>>
        GetSelectAsync(CancellationToken cancellationToken = default)
    {
        var response = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear => fiscalYear.CompanyId == companyId)
            .OrderByDescending(fiscalYear => fiscalYear.IsCurrent)
            .ThenByDescending(fiscalYear => fiscalYear.StartDate)
            .ThenByDescending(fiscalYear => fiscalYear.Id)
            .ProjectToType<FiscalYearSelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<FiscalYearSelectResponse>>.Success(
            response);
    }

    public async Task<Result<FiscalYearResponse>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await ProjectResponseQuery(isCurrent: true)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<FiscalYearResponse>.Failure(CurrentNotFound())
            : Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<FiscalYearResponse>.Failure(NotFound(id))
            : Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> AddAsync(
        FiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate >= request.EndDate)
        {
            return Result<FiscalYearResponse>.Failure(DateRangeInvalid());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await NameExistsAsync(
                request.Name,
                excludedId: null,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(NameExists(request.Name.Trim()));
        }

        if (await DateRangeOverlapsAsync(
                request.StartDate,
                request.EndDate,
                excludedId: null,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(DateRangeOverlaps());
        }

        var fiscalYear = request.Adapt<FiscalYear>();
        fiscalYear.CompanyId = companyId;
        fiscalYear.Status = FiscalYearStatus.Open;

        var hasCurrent = await dbContext.FiscalYears
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    fiscalYear.IsCurrent,
                cancellationToken);

        if (request.IsCurrent || !hasCurrent)
        {
            await ClearCurrentAsync(null, cancellationToken);
            fiscalYear.IsCurrent = true;
        }

        dbContext.FiscalYears.Add(fiscalYear);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(fiscalYear.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> UpdateAsync(
        int id,
        FiscalYearUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<FiscalYearResponse>.Failure(RowVersionRequired());
        }

        if (request.StartDate >= request.EndDate)
        {
            return Result<FiscalYearResponse>.Failure(DateRangeInvalid());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (fiscalYear.Status == FiscalYearStatus.Closed)
        {
            return Result<FiscalYearResponse>.Failure(
                ClosedCannotBeModified());
        }

        if (await NameExistsAsync(
                request.Name,
                excludedId: id,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(NameExists(request.Name.Trim()));
        }

        if (await DateRangeOverlapsAsync(
                request.StartDate,
                request.EndDate,
                excludedId: id,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(DateRangeOverlaps());
        }

        if (!fiscalYear.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<FiscalYearResponse>.Failure(Concurrency());
        }

        if (request.IsCurrent)
        {
            await ClearCurrentAsync(id, cancellationToken);
        }

        var entry = dbContext.Entry(fiscalYear);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        request.Adapt(fiscalYear);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<FiscalYearResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> CloseAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            id,
            FiscalYearStatus.Closed,
            cancellationToken);
    }

    public async Task<Result<FiscalYearResponse>> ReopenAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            id,
            FiscalYearStatus.Open,
            cancellationToken);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (fiscalYear.Status == FiscalYearStatus.Closed)
        {
            return Result.Failure(ClosedCannotBeDeleted());
        }

        if (fiscalYear.IsCurrent)
        {
            return Result.Failure(CurrentCannotBeDeleted());
        }

        if (await dbContext.FinancialStatementLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.FiscalYearId == id,
                    cancellationToken) ||
            await dbContext.AccountStatementMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.FiscalYearId == id,
                    cancellationToken) ||
            await dbContext.AccountMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.FiscalYearId == id,
                    cancellationToken))
        {
            return Result.Failure(HasAccountingSetup());
        }

        dbContext.FiscalYears.Remove(fiscalYear);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<FiscalYearResponse>> ChangeStatusAsync(
        int id,
        FiscalYearStatus status,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (fiscalYear.Status == status)
        {
            return Result<FiscalYearResponse>.Failure(
                status == FiscalYearStatus.Closed
                    ? AlreadyClosed()
                    : AlreadyOpen());
        }

        if (status == FiscalYearStatus.Closed)
        {
            var readiness = accountingReadinessService is null
                ? null
                : await accountingReadinessService.GetAsync(
                    fiscalYear.Id,
                    cancellationToken);
            if (readiness is { IsFailure: true })
            {
                return Result<FiscalYearResponse>.Failure(readiness.Errors);
            }

            if (readiness is { Value.IsReady: false })
            {
                var errors = new List<Error>
                {
                    ClosingNotReady(
                        fiscalYear.Name,
                        readiness.Value.Issues.Count)
                };
                errors.AddRange(readiness.Value.Issues.Select(issue =>
                    Error.Conflict(
                        "FiscalYears.ClosingIssue",
                        issue.Message,
                        issue.IssueType)));
                return Result<FiscalYearResponse>.Failure(errors);
            }

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var transfer = await TransferClosingBalancesAsync(
                fiscalYear,
                cancellationToken);
            if (transfer.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<FiscalYearResponse>.Failure(transfer.Errors);
            }

            fiscalYear.Status = status;
            fiscalYear.ClosedOn = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var closedResponse = await ProjectResponseQuery(id)
                .FirstAsync(cancellationToken);
            return Result<FiscalYearResponse>.Success(closedResponse);
        }

        fiscalYear.Status = status;
        fiscalYear.ClosedOn = status == FiscalYearStatus.Closed
            ? timeProvider.GetUtcNow().UtcDateTime
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    private async Task<Result> TransferClosingBalancesAsync(
        FiscalYear fiscalYear,
        CancellationToken cancellationToken)
    {
        var nextYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate > fiscalYear.EndDate)
            .OrderBy(year => year.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        // A terminal year has no destination for opening balances.
        if (nextYear is null)
        {
            return Result.Success();
        }
        if (nextYear.Status != FiscalYearStatus.Open)
        {
            return Result.Failure(NextFiscalYearClosed(nextYear.Name));
        }

        var existingTransfer = await dbContext.JournalEntries
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry =>
                entry.CompanyId == companyId &&
                entry.EntryType == JournalEntryType.Opening &&
                entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                entry.SourceId == fiscalYear.Id,
                cancellationToken);

        var balances = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.JournalEntry.FiscalYearId == fiscalYear.Id &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.Account.AccountType != AccountType.Revenue &&
                line.Account.AccountType != AccountType.Expense)
            .GroupBy(line => new { line.AccountId, line.Account.Code, line.Account.Name })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.Code,
                group.Key.Name,
                Balance = group.Sum(line => line.Debit - line.Credit)
            })
            .Where(row => row.Balance != 0m)
            .ToListAsync(cancellationToken);

        if (balances.Count == 0)
        {
            if (existingTransfer is not null)
            {
                dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
                dbContext.JournalEntries.Remove(existingTransfer);
            }
            return Result.Success();
        }

        var lines = balances
            .Select(balance => new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = balance.AccountId,
                Description = $"ترحيل رصيد {balance.Code} - {balance.Name}",
                Debit = balance.Balance > 0m ? balance.Balance : 0m,
                Credit = balance.Balance < 0m ? -balance.Balance : 0m
            })
            .ToList();
        var net = lines.Sum(line => line.Debit - line.Credit);
        if (net != 0m)
        {
            var equityAccountId = await dbContext.AccountMappings
                .AsNoTracking()
                .Where(mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FiscalYearId == nextYear.Id &&
                    mapping.MappingType == AccountingMappingType.OpeningBalanceEquity &&
                    mapping.SourceId == null)
                .Select(mapping => (int?)mapping.AccountId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!equityAccountId.HasValue)
            {
                return Result.Failure(
                    OpeningBalanceAccountMissing(nextYear.Name));
            }

            lines.Add(new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = equityAccountId.Value,
                Description = "مقابل ترحيل أرصدة المركز المالي",
                Debit = net < 0m ? -net : 0m,
                Credit = net > 0m ? net : 0m
            });
        }

        if (existingTransfer is not null)
        {
            dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
            existingTransfer.FiscalYearId = nextYear.Id;
            existingTransfer.EntryDate = nextYear.StartDate;
            existingTransfer.Description = $"أرصدة افتتاحية مرحّلة من السنة المالية {fiscalYear.Name}";
            existingTransfer.SourceNumber = fiscalYear.Name;
            existingTransfer.PostedOn = timeProvider.GetUtcNow().UtcDateTime;
            existingTransfer.Lines = lines;
            return Result.Success();
        }

        var entryNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "OB",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.CompanyId == companyId)
                .Select(entry => entry.EntryNumber),
            cancellationToken);
        dbContext.JournalEntries.Add(new JournalEntry
        {
            CompanyId = companyId,
            FiscalYearId = nextYear.Id,
            EntryNumber = entryNumber,
            EntryDate = nextYear.StartDate,
            Description = $"أرصدة افتتاحية مرحّلة من السنة المالية {fiscalYear.Name}",
            EntryType = JournalEntryType.Opening,
            SourceType = JournalEntrySourceType.FiscalYearClosing,
            SourceId = fiscalYear.Id,
            SourceNumber = fiscalYear.Name,
            Status = JournalEntryStatus.Posted,
            PostedOn = timeProvider.GetUtcNow().UtcDateTime,
            Lines = lines
        });
        return Result.Success();
    }

    private IQueryable<FiscalYearResponse> ProjectResponseQuery(
        int? id = null,
        bool? isCurrent = null) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear =>
                fiscalYear.CompanyId == companyId &&
                (!id.HasValue || fiscalYear.Id == id.Value) &&
                (!isCurrent.HasValue ||
                 fiscalYear.IsCurrent == isCurrent.Value))
            .ProjectToType<FiscalYearResponse>();

    private Task<bool> NameExistsAsync(
        string name,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    (!excludedId.HasValue ||
                     fiscalYear.Id != excludedId.Value) &&
                    fiscalYear.Name.ToUpper() == name.Trim().ToUpper(),
                cancellationToken);

    private Task<bool> DateRangeOverlapsAsync(
        DateOnly startDate,
        DateOnly endDate,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    (!excludedId.HasValue ||
                     fiscalYear.Id != excludedId.Value) &&
                    fiscalYear.StartDate <= endDate &&
                    fiscalYear.EndDate >= startDate,
                cancellationToken);

    private Task<int> ClearCurrentAsync(
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .Where(fiscalYear =>
                fiscalYear.CompanyId == companyId &&
                fiscalYear.IsCurrent &&
                (!excludedId.HasValue ||
                 fiscalYear.Id != excludedId.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    fiscalYear => fiscalYear.IsCurrent,
                    false),
                cancellationToken);
}
