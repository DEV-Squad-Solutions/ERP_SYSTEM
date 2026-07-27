using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherResponse(
    int Id,
    int CompanyId,
    string VoucherNumber,
    DateOnly VoucherDate,
    CashDirection Direction,
    int CashboxId,
    string CashboxName,
    int CashMovementTypeId,
    string CashMovementTypeName,
    PartnerAccountEffect PartnerEffect,
    CashPartyType PartyType,
    int? BusinessPartnerId,
    string? BusinessPartnerName,
    int? DriverId,
    string? DriverName,
    int? DriverTripId,
    string? DriverTripInvoiceNumber,
    string? ExternalPartyName,
    decimal Amount,
    CurrencyCode Currency,
    string? ReferenceNumber,
    string? Description,
    string? Notes,
    byte[] RowVersion);
