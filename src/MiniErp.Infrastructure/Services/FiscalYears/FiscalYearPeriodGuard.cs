using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearPeriodGuard(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IFiscalYearPeriodGuard, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> EnsureOpenForFiscalYearAsync(
        int fiscalYearId,
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year => year.CompanyId == companyId && year.Id == fiscalYearId)
            .Select(year => new { year.Name, year.StartDate, year.EndDate, year.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(DateNotCovered(date, fieldName));
        }
        if (date < fiscalYear.StartDate || date > fiscalYear.EndDate)
        {
            return Result.Failure(QueryDateOutsideRange(
                date: date,
                fieldName: fieldName,
                fiscalYearName: fiscalYear.Name,
                startDate: fiscalYear.StartDate,
                endDate: fiscalYear.EndDate));
        }
        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result.Success()
            : Result.Failure(Closed(date, fiscalYear.Name, fieldName));
    }

    public async Task<Result> EnsureOpenAsync(
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.IsCurrent)
            .Select(year => new
            {
                year.Name,
                year.StartDate,
                year.EndDate,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result.Failure(DateNotCovered(date, fieldName));
        }

        if (date < fiscalYear.StartDate || date > fiscalYear.EndDate)
        {
            return Result.Failure(QueryDateOutsideRange(
                date: date,
                fieldName: fieldName,
                fiscalYearName: fiscalYear.Name,
                startDate: fiscalYear.StartDate,
                endDate: fiscalYear.EndDate));
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result.Success()
            : Result.Failure(Closed(date, fiscalYear.Name, fieldName));
    }
}
