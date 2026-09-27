using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearQueryScopeResolver(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IFiscalYearQueryScopeResolver, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<FiscalYearQueryScope>> ResolveAsync(
        int? fiscalYearId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (fiscalYearId is <= 0)
        {
            return Result<FiscalYearQueryScope>.Failure(InvalidId());
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            toDate.Value < fromDate.Value)
        {
            return Result<FiscalYearQueryScope>.Failure(
                QueryDateRangeInvalid());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                (fiscalYearId.HasValue
                    ? year.Id == fiscalYearId.Value
                    : year.IsCurrent))
            .Select(year => new FiscalYearQueryScope(
                FiscalYearId: year.Id,
                FiscalYearName: year.Name,
                StartDate: year.StartDate,
                EndDate: year.EndDate,
                Status: year.Status,
                IsCurrent: year.IsCurrent))
            .SingleOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result<FiscalYearQueryScope>.Failure(
                fiscalYearId.HasValue
                    ? NotFound(fiscalYearId.Value)
                    : CurrentNotFound());
        }

        if (fromDate.HasValue &&
            !Contains(fiscalYear, fromDate.Value))
        {
            return Result<FiscalYearQueryScope>.Failure(
                QueryDateOutsideRange(
                    date: fromDate.Value,
                    fieldName: "FromDate",
                    fiscalYearName: fiscalYear.FiscalYearName,
                    startDate: fiscalYear.StartDate,
                    endDate: fiscalYear.EndDate));
        }

        if (toDate.HasValue &&
            !Contains(fiscalYear, toDate.Value))
        {
            return Result<FiscalYearQueryScope>.Failure(
                QueryDateOutsideRange(
                    date: toDate.Value,
                    fieldName: "ToDate",
                    fiscalYearName: fiscalYear.FiscalYearName,
                    startDate: fiscalYear.StartDate,
                    endDate: fiscalYear.EndDate));
        }

        return Result<FiscalYearQueryScope>.Success(fiscalYear);
    }

    private static bool Contains(
        FiscalYearQueryScope fiscalYear,
        DateOnly date) =>
        date >= fiscalYear.StartDate && date <= fiscalYear.EndDate;
}
