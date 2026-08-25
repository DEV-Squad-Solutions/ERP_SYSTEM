using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeTransactionResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes,
    decimal RunningBalance,
    EmployeeTransactionSource SourceType,
    int? SourceId,
    int CashVoucherId,
    string? CashVoucherNumber,
    int CashBoxId,
    string? CashboxName);

public sealed record EmployeeAccountBalanceResponse(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal Balance);

public sealed record BulkEmployeeTransactionSummary(
    int TotalProcessed,
    int SuccessCount,
    int FailureCount,
    decimal TotalAmount);

public sealed record BulkEmployeeTransactionResponse(
    IReadOnlyList<EmployeeTransactionResponse> Items,
    BulkEmployeeTransactionSummary Summary);
