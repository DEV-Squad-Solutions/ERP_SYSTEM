using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.MonetaryAccountRevaluations;

public interface IMonetaryAccountRevaluationService : IScopedService
{
    Task<Result<IReadOnlyList<MonetaryAccountRevaluationOption>>> GetOptionsAsync(
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default);

    Task<Result<MonetaryAccountRevaluationResponse>> CreateAsync(
        MonetaryAccountRevaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MonetaryAccountRevaluationResponse>>> GetAsync(
        int? accountId = null,
        JournalPartyType? partyType = null,
        int? partyId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default);
}
