using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeStatementItem(
    int TransactionId,
    DateOnly TransactionDate,
    EmployeeTransactionType Type,
    decimal Amount,
    decimal Credit,
    decimal Debit,
    decimal RunningBalance,
    string SourceType,
    int? SourceId,
    int CashVoucherId,
    string CashVoucherNumber,
    int CashBoxId,
    string CashboxName,
    string? Notes);

public sealed record EmployeeStatementSummary(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal TotalSalaryCredit,
    decimal TotalCashWithdrawal,
    decimal ClosingBalance,
    int TotalTransactions);

public sealed record EmployeeStatementResponse(
    EmployeeStatementSummary Summary,
    IReadOnlyList<EmployeeStatementItem> Transactions);
