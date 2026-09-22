using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.MonetaryAccountRevaluations;

public sealed record MonetaryAccountRevaluationRequest(
    int AccountId,
    CurrencyCode Currency,
    JournalPartyType? PartyType,
    int? PartyId,
    DateOnly RevaluationDate,
    decimal ClosingRate);

public sealed record MonetaryAccountRevaluationResponse(
    int Id,
    int AccountId,
    string AccountCode,
    string AccountName,
    CurrencyCode Currency,
    JournalPartyType? PartyType,
    int? PartyId,
    string? PartyCode,
    string? PartyName,
    DateOnly RevaluationDate,
    decimal ClosingRate,
    decimal ForeignAmount,
    decimal CarryingBaseAmount,
    decimal TargetBaseAmount,
    decimal DeltaBaseAmount,
    int? JournalEntryId,
    string JournalEntryNumber);

public sealed record MonetaryAccountRevaluationOption(
    int AccountId,
    string AccountCode,
    string AccountName,
    CurrencyCode Currency,
    JournalPartyType? PartyType,
    int? PartyId,
    string? PartyCode,
    string? PartyName);
