using System.Data;
using System.Globalization;
using Mapster;
using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed partial class FiscalYearService
{
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

    public async Task<Result<FiscalYearResponse>> SetCurrentAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                year =>
                    year.CompanyId == companyId &&
                    year.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (!fiscalYear.IsCurrent)
        {
            await ClearCurrentAsync(id, cancellationToken);
            fiscalYear.IsCurrent = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
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
        await EnsureExchangeRateCarryForwardAsync(
            fiscalYear,
            cancellationToken);

        if (defaultAccountingSetupService is not null)
        {
            await defaultAccountingSetupService.EnsureFiscalYearAsync(
                companyId,
                fiscalYear.Id,
                cancellationToken);
        }

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

        if (request.StartDate != fiscalYear.StartDate &&
            (await dbContext.DriverOpeningBalances.AnyAsync(row =>
                row.CompanyId == companyId && row.FiscalYearId == id, cancellationToken) ||
             await DriverOpeningBalanceLedgerQueries.CarriedLines(dbContext, companyId, id)
                 .AnyAsync(cancellationToken)))
        {
            return Result<FiscalYearResponse>.Failure(DriverOpeningBalanceStartDateLocked());
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

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

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
            var nextStartDate = fiscalYear.StartDate.AddYears(1);
            var nextEndDate = fiscalYear.EndDate.AddYears(1);
            var nextYear = await dbContext.FiscalYears
                .Where(year =>
                    year.CompanyId == companyId &&
                    year.StartDate == nextStartDate &&
                    year.EndDate == nextEndDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (nextYear is null)
            {
                if (await DateRangeOverlapsAsync(
                        nextStartDate,
                        nextEndDate,
                        excludedId: null,
                        cancellationToken))
                {
                    return Result<FiscalYearResponse>.Failure(
                        DateRangeOverlaps());
                }

                nextYear = new FiscalYear
                {
                    CompanyId = companyId,
                    Name = await GenerateNextFiscalYearNameAsync(
                        fiscalYear,
                        cancellationToken),
                    StartDate = nextStartDate,
                    EndDate = nextEndDate,
                    Status = FiscalYearStatus.Open,
                    IsCurrent = false
                };
                dbContext.FiscalYears.Add(nextYear);
                await dbContext.SaveChangesAsync(cancellationToken);

                if (defaultAccountingSetupService is not null)
                {
                    await defaultAccountingSetupService.EnsureFiscalYearAsync(
                        companyId,
                        nextYear.Id,
                        cancellationToken);
                }
            }
            // Check before carrying rates forward: writing a rate into a
            // closed year is rejected by the persistence guard as an
            // exception instead of this documented result.
            if (nextYear.Status != FiscalYearStatus.Open)
            {
                return Result<FiscalYearResponse>.Failure(
                    NextFiscalYearClosed(nextYear.Name));
            }

            await EnsureExchangeRateCarryForwardAsync(
                nextYear,
                cancellationToken);

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

            var transfer = await TransferClosingBalancesAsync(
                fiscalYear,
                nextYear,
                cancellationToken);
            if (transfer.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<FiscalYearResponse>.Failure(transfer.Errors);
            }

            if (inventoryCarryForwardService is not null)
            {
                var inventoryTransfer = await inventoryCarryForwardService
                    .CarryForwardAsync(
                        sourceFiscalYearId: fiscalYear.Id,
                        targetFiscalYearId: nextYear.Id,
                        targetStartDate: nextYear.StartDate,
                        sourceFiscalYearName: fiscalYear.Name,
                        cancellationToken: cancellationToken);
                if (inventoryTransfer.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<FiscalYearResponse>.Failure(
                        inventoryTransfer.Errors);
                }
            }

            fiscalYear.Status = status;
            fiscalYear.ClosedOn = timeProvider.GetUtcNow().UtcDateTime;
            if (fiscalYear.IsCurrent)
            {
                fiscalYear.IsCurrent = false;
                nextYear.IsCurrent = true;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var closedResponse = await ProjectResponseQuery(id)
                .FirstAsync(cancellationToken);
            return Result<FiscalYearResponse>.Success(closedResponse);
        }

        var laterClosedYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate > fiscalYear.EndDate &&
                year.Status == FiscalYearStatus.Closed)
            .OrderBy(year => year.StartDate)
            .Select(year => year.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (laterClosedYear is not null)
        {
            return Result<FiscalYearResponse>.Failure(
                LaterFiscalYearClosed(laterClosedYear));
        }

        var existingTransfer = await dbContext.JournalEntries
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry =>
                entry.CompanyId == companyId &&
                entry.EntryType == JournalEntryType.Opening &&
                entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                entry.SourceId == fiscalYear.Id,
                cancellationToken);
        if (existingTransfer is not null)
        {
            dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
            dbContext.JournalEntries.Remove(existingTransfer);
        }

        fiscalYear.Status = status;
        fiscalYear.ClosedOn = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }
}
