using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record CashboxStatementItemResponse(
    int CashVoucherId,
    DateOnly Date,
    string VoucherNumber,
    string MovementName,
    string? Description,
    string? PartyName,
    decimal ReceiptAmount,
    decimal PaymentAmount,
    decimal Balance,
    string? ReferenceNumber);

public sealed record CashboxStatementSummaryResponse(
    decimal OpeningBalance,
    decimal TotalReceipts,
    decimal TotalPayments,
    decimal ClosingBalance);

public sealed record CashboxStatementResponse(
    int CashboxId,
    string CashboxName,
    CurrencyCode Currency,
    IReadOnlyList<CashboxStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    CashboxStatementSummaryResponse Summary);

public sealed record PartnerStatementItemResponse(
    DateOnly Date,
    string DocumentNumber,
    string MovementName,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal BalanceAmount,
    string BalanceDescription,
    string? ReferenceNumber);

public sealed record PartnerStatementSummaryResponse(
    decimal OpeningBalanceAmount,
    string OpeningBalanceDescription,
    decimal ClosingBalanceAmount,
    string ClosingBalanceDescription);

public sealed record PartnerStatementResponse(
    int BusinessPartnerId,
    string BusinessPartnerName,
    CurrencyCode Currency,
    IReadOnlyList<PartnerStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    PartnerStatementSummaryResponse Summary);

public sealed record DriverStatementItemResponse(
    int SourceId,
    DateOnly Date,
    string DocumentNumber,
    string SourceName,
    string? InvoiceNumber,
    int? DriverTripId,
    string? DriverTripNumber,
    string MovementName,
    string? Description,
    decimal AmountPaidToDriver,
    decimal AmountReceivedFromDriver,
    decimal TripCost,
    decimal BalanceAmount,
    string BalanceDescription,
    string? CashboxName,
    string? ReferenceNumber);

public sealed record DriverStatementSummaryResponse(
    decimal OpeningBalanceAmount,
    string OpeningBalanceDescription,
    decimal TotalPaidToDriver,
    decimal TotalReceivedFromDriver,
    decimal TotalTripCost,
    decimal ClosingBalanceAmount,
    string ClosingBalanceDescription);

public sealed record DriverStatementResponse(
    int DriverId,
    string DriverName,
    IReadOnlyList<DriverStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DriverStatementSummaryResponse Summary);
