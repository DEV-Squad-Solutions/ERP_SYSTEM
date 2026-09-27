using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherResponse(
    int Id,
    int CompanyId,
    string VoucherNumber,
    DateOnly VoucherDate,
    CashDirection Direction,
    int? CashboxId,
    string? CashboxName,
    int? CashMovementTypeId,
    string? CashMovementTypeName,
    CashMovementClassification? Classification,
    CashPartyType PartyType,
    int? EmployeeId,
    string? EmployeeName,
    int? BusinessPartnerId,
    string? BusinessPartnerName,
    int? DriverId,
    string? DriverName,
    int? DriverTripId,
    string? DriverTripInvoiceNumber,
    string? ExternalPartyName,
    decimal Amount,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    string? ReferenceNumber,
    string? Description,
    string? Notes,
    byte[] RowVersion)
{
    public int FiscalYearId { get; init; }

    public string FiscalYearName { get; init; } = string.Empty;

    public bool IsDraft { get; init; }

    /// <summary>
    /// Indicates that this row is a read-only synthetic opening-balance row.
    /// Opening balances are stored on the cashbox and are intentionally not
    /// persisted as CashVoucher records.
    /// </summary>
    public bool IsOpeningBalance { get; init; }

    /// <summary>
    /// Current cashbox balance in the voucher/cashbox currency. Draft vouchers
    /// are excluded because they have no financial effect.
    /// </summary>
    public decimal? CashboxBalance { get; init; }

    public int? InvoiceId { get; init; }

    public int? CashboxTransferId { get; init; }

    public string? InvoiceNumber { get; init; }

    public decimal? AppliedInvoiceAmount { get; init; }

    public CurrencyCode? AppliedInvoiceCurrency { get; init; }

    public decimal? AppliedBaseAmount { get; init; }

    public decimal? RealizedExchangeDifference { get; init; }

    public int? AccountId { get; init; }

    public string? AccountCode { get; init; }

    public string? AccountName { get; init; }

    public AccountType? AccountType { get; init; }
}
