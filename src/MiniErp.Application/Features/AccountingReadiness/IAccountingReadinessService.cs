using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.AccountingReadiness;

public interface IAccountingReadinessService : IScopedService
{
    Task<Result<AccountingReadinessResponse>> GetAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingBackfillResponse>> BackfillAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default);
}
