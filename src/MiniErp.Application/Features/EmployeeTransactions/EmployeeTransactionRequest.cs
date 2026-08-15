using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

/// <summary>
/// Request to post a manual ledger entry to an employee's account.
/// For cash-backed types (Withdrawal, Advance) use <see cref="EmployeeWithdrawalRequest"/>.
/// </summary>
public sealed record EmployeeAccountEntryRequest(
    int EmployeeId,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes = null);

/// <summary>
/// Request to withdraw cash from an employee's account.
/// Creates a CashVoucher (Payment) and debits the employee account atomically.
/// </summary>
public sealed record EmployeeWithdrawalRequest(
    int EmployeeId,
    decimal Amount,
    DateOnly TransactionDate,
    int CashboxId,
    int CashMovementTypeId,
    EmployeeTransactionType Type = EmployeeTransactionType.Withdrawal,
    string? Notes = null);
