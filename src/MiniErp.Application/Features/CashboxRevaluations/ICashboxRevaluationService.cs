using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashboxRevaluations;

public interface ICashboxRevaluationService : IScopedService
{
    Task<Result<CashboxRevaluationResponse>> CreateAsync(
        CashboxRevaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CashboxRevaluationResponse>>> GetAsync(
        int? cashboxId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
}
