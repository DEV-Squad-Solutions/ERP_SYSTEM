using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeTransactionResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeName,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes,
    decimal RunningBalance,
    EmployeeTransactionSource SourceType,
    int? SourceId,
    int? CashVoucherId);

public sealed record EmployeeAccountBalanceResponse(
    int EmployeeId,
    string EmployeeName,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal Balance);
