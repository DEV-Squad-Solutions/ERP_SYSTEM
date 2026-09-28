using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IFiscalYearQueryScopeResolver
{
    Task<Result<FiscalYearQueryScope>> ResolveAsync(
        int? fiscalYearId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
}
