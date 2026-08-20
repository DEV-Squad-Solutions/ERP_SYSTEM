using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherFilterRequest(
    string? Search = null,
    string? VoucherNumber = null,
    CashDirection? Direction = null,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    CashMovementClassification? Classification = null,
    CashPartyType? PartyType = null,
    int? BusinessPartnerId = null,
    int? DriverId = null,
    int? DriverTripId = null,
    bool? IsDraft = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? EmployeeId = null)
{
    public const int VoucherNumberMaximumLength = 100;
}
