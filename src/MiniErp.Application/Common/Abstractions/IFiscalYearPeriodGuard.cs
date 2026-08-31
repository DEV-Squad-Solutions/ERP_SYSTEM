using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IFiscalYearPeriodGuard
{
    Task<Result> EnsureOpenAsync(
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default);
}
