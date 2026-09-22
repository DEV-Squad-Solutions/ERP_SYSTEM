using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashboxRevaluations;

public sealed record CashboxRevaluationRequest(
    int CashboxId,
    DateOnly RevaluationDate,
    decimal ClosingRate);

public sealed record CashboxRevaluationResponse(
    int Id,
    int CashboxId,
    string CashboxName,
    CurrencyCode Currency,
    DateOnly RevaluationDate,
    decimal ClosingRate,
    decimal ForeignAmount,
    decimal CarryingBaseAmount,
    decimal TargetBaseAmount,
    decimal DeltaBaseAmount,
    int? JournalEntryId,
    string JournalEntryNumber);
