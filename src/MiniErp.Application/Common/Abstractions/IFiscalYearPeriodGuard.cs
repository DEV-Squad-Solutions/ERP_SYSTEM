using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IFiscalYearPeriodGuard
{
    Task<Result> EnsureOpenAsync(
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default);

    // Internal replay can affect a different open year. Custom policy guards
    // retain their existing behavior unless they explicitly specialize it.
    Task<Result> EnsureOpenForFiscalYearAsync(
        int fiscalYearId,
        DateOnly date,
        string fieldName,
        CancellationToken cancellationToken = default) =>
        EnsureOpenAsync(date, fieldName, cancellationToken);
}
