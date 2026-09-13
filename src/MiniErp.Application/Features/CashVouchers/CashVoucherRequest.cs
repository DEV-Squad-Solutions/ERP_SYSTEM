using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherRequest(
    DateOnly VoucherDate,
    CashDirection Direction,
    int CashboxId,
    decimal Amount,
    string? Description,
    int? CashMovementTypeId = null,
    int? EmployeeId = null,
    int? BusinessPartnerId = null,
    int? DriverId = null,
    int? DriverTripId = null,
    string? ExternalPartyName = null,
    string? ReferenceNumber = null,
    string? Notes = null,
    decimal? ExchangeRate = null,
    int? AccountId = null,
    EmployeeMovementType? EmployeeMovementType = null)
{
    public const int ExternalPartyNameMaximumLength = 200;

    public const int ReferenceNumberMaximumLength = 100;

    public const int DescriptionMaximumLength = 1_000;

    public const int NotesMaximumLength = 1_000;
}

public sealed record CashVoucherUpdateRequest(
    DateOnly VoucherDate,
    CashDirection Direction,
    int? CashboxId,
    int? CashMovementTypeId,
    int? EmployeeId,
    int? BusinessPartnerId,
    int? DriverId,
    int? DriverTripId,
    string? ExternalPartyName,
    decimal Amount,
    string? ReferenceNumber,
    string? Description,
    string? Notes,
    byte[]? RowVersion,
    decimal? ExchangeRate = null,
    int? AccountId = null,
    EmployeeMovementType? EmployeeMovementType = null)
;
