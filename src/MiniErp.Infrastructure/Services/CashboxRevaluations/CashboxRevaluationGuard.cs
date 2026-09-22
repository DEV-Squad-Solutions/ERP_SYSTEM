using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.CashboxRevaluations.CashboxRevaluationErrors;

namespace MiniErp.Infrastructure.Services.CashboxRevaluations;

internal static class CashboxRevaluationGuard
{
    internal static async Task<Error?> ValidateAsync(
        ApplicationDbContext dbContext,
        int companyId,
        DateOnly entryDate,
        IEnumerable<int> cashboxIds,
        CancellationToken cancellationToken)
    {
        var ids = cashboxIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return null;
        }

        var revaluationDate = await dbContext.CashboxRevaluations
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId &&
                ids.Contains(row.CashboxId) &&
                row.RevaluationDate >= entryDate &&
                !row.IsDeleted)
            .OrderBy(row => row.RevaluationDate)
            .Select(row => (DateOnly?)row.RevaluationDate)
            .FirstOrDefaultAsync(cancellationToken);
        return revaluationDate.HasValue
            ? PostingBlocked(revaluationDate.Value)
            : null;
    }
}
