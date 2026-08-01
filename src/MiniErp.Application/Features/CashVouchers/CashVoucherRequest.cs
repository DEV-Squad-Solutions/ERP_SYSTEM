using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherRequest(
    string? VoucherNumber = null,
    DateOnly VoucherDate = default,
    CashDirection Direction = default,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    CashPartyType? PartyType = null,
    int? BusinessPartnerId = null,
    int? DriverId = null,
    int? DriverTripId = null,
    string? ExternalPartyName = null,
    decimal Amount = 0m,
    string? ReferenceNumber = null,
    string? Description = null,
    string? Notes = null,
    decimal? ExchangeRate = null)
{
    public const int VoucherNumberMaximumLength = 100;

    public const int ExternalPartyNameMaximumLength = 200;

    public const int ReferenceNumberMaximumLength = 100;

    public const int DescriptionMaximumLength = 1_000;

    public const int NotesMaximumLength = 1_000;
}

public sealed record CashVoucherUpdateRequest(
    string? VoucherNumber,
    DateOnly VoucherDate,
    CashDirection Direction,
    int? CashboxId,
    int? CashMovementTypeId,
    CashPartyType? PartyType,
    int? BusinessPartnerId,
    int? DriverId,
    int? DriverTripId,
    string? ExternalPartyName,
    decimal Amount,
    string? ReferenceNumber,
    string? Description,
    string? Notes,
    byte[]? RowVersion,
    decimal? ExchangeRate = null);
