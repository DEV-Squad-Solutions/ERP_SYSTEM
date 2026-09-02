using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
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

        fiscalYear.Status = status;
        fiscalYear.ClosedOn = status == FiscalYearStatus.Closed
            ? timeProvider.GetUtcNow().UtcDateTime
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
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
