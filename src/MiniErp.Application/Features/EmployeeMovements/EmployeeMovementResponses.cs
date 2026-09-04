using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeMovements;

public sealed record EmployeeMovementResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeMovementType Type,
    DateOnly MovementDate,
    CurrencyCode Currency,
    decimal Amount,
    decimal Debit,
    decimal Credit,
    decimal ExchangeRate,
    decimal BaseDebit,
    decimal BaseCredit,
    int? CashVoucherId,
    string? CashVoucherNumber,
    string? Notes,
    DateTime CreatedOn);

public sealed record EmployeeMovementReportItemResponse(
    int Id,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    DateOnly Date,
    EmployeeMovementType MovementType,
    string MovementTypeName,
    decimal OriginalAmount,
    CurrencyCode Currency,
    decimal ExchangeRate,
    decimal EgpAmount,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? Reason,
    string? Notes,
    int? CashVoucherId,
    string? CashVoucherNumber,
    string? CashVoucherReference);

public sealed record EmployeeMovementReportSummaryResponse(
    decimal TotalDebits,
    decimal TotalCredits,
    decimal NetBalance,
    decimal TotalAdvances,
    decimal TotalWithdrawals,
    decimal TotalBonuses,
    decimal TotalDeductions,
    int TotalMovements);

public sealed record EmployeeMovementReportResponse(
    EmployeeMovementReportSummaryResponse Summary,
    IReadOnlyList<EmployeeMovementReportItemResponse> Items);

