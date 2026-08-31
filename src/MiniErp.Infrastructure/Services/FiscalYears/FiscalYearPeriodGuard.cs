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

    public async Task<Result> EnsureOpenAsync(
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= date &&
                year.EndDate >= date)
            .Select(year => new
            {
                year.Name,
                year.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result.Failure(DateNotCovered(date, fieldName));
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result.Success()
            : Result.Failure(Closed(date, fiscalYear.Name, fieldName));
    }
}
