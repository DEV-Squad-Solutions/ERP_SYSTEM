using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record CashboxStatementItemResponse(
    int CashVoucherId,
    DateOnly Date,
    string VoucherNumber,
    CashDirection Direction,
    int CashMovementTypeId,
    string CashMovementTypeName,
    string? Description,
    CashPartyType PartyType,
    string? PartyName,
    decimal ReceivedAmount,
    decimal PaidAmount,
    decimal RunningBalance,
    string? ReferenceNumber);

public sealed record CashboxStatementSummaryResponse(
    decimal OpeningBalance,
    decimal TotalReceipts,
    decimal TotalPayments,
    decimal ClosingBalance);

public sealed record CashboxStatementResponse(
    IReadOnlyList<CashboxStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    CashboxStatementSummaryResponse Summary);

public sealed record PartnerStatementItemResponse(
    int SourceId,
    PartnerStatementSourceType SourceType,
    DateOnly Date,
    string DocumentNumber,
    BusinessPartnerMovementType? MovementType,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? ReferenceNumber);

public sealed record PartnerStatementSummaryResponse(
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance);

public sealed record PartnerStatementResponse(
    IReadOnlyList<PartnerStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    PartnerStatementSummaryResponse Summary);

public sealed record DriverStatementItemResponse(
    int SourceId,
    DriverStatementSourceType SourceType,
    DateOnly Date,
    string SourceNumber,
    string? InvoiceNumber,
    int? DriverTripId,
    string? DriverTripNumber,
    string? MovementTypeName,
    string? Description,
    decimal CashPaidToDriver,
    decimal CashReceivedFromDriver,
    decimal DriverTripCost,
    decimal RunningBalance,
    string? CashboxName,
    string? ReferenceNumber);

public sealed record DriverStatementSummaryResponse(
    decimal OpeningBalance,
    decimal TotalCashPaid,
    decimal TotalCashReceived,
    decimal TotalTripCost,
    decimal ClosingBalance);

public sealed record DriverStatementResponse(
    IReadOnlyList<DriverStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DriverStatementSummaryResponse Summary);
