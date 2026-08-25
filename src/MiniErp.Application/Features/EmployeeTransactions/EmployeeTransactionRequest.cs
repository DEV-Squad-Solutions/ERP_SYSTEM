using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

/// <summary>
/// Request to post a single ledger entry (Credit, Debit, Bonus, Deduction) to an employee's account.
/// Every transaction is backed by a Cash Voucher and an active Cashbox.
/// </summary>
public sealed record EmployeeAccountEntryRequest(
    int EmployeeId,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    int CashboxId,
    int CashMovementTypeId,
    string? Notes = null);

/// <summary>
/// Individual item inside a bulk employee account entry request.
/// </summary>
public sealed record IndividualEmployeeAccountEntryRequest(
    int EmployeeId,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly? TransactionDate = null,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    string? Notes = null);

/// <summary>
/// Request to post multiple ledger entries to employee accounts in a single batch.
/// </summary>
public sealed record BulkEmployeeAccountEntryRequest(
    IReadOnlyList<IndividualEmployeeAccountEntryRequest> Entries,
    DateOnly? DefaultTransactionDate = null,
    int? DefaultCashboxId = null,
    int? DefaultCashMovementTypeId = null);

/// <summary>
/// Request to withdraw cash or take an advance from an employee's account.
/// Generates a CashVoucher (Payment) and debits the employee account.
/// </summary>
public sealed record EmployeeWithdrawalRequest(
    int EmployeeId,
    decimal Amount,
    DateOnly TransactionDate,
    int CashboxId,
    int CashMovementTypeId,
    EmployeeTransactionType Type = EmployeeTransactionType.Withdrawal,
    string? Notes = null);

/// <summary>
/// Individual item inside a bulk employee withdrawal/advance request.
/// </summary>
public sealed record IndividualEmployeeWithdrawalRequest(
    int EmployeeId,
    decimal Amount,
    DateOnly? TransactionDate = null,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    EmployeeTransactionType Type = EmployeeTransactionType.Withdrawal,
    string? Notes = null);

/// <summary>
/// Request to process bulk cash withdrawals or advances for multiple employees.
/// </summary>
public sealed record BulkEmployeeWithdrawalRequest(
    IReadOnlyList<IndividualEmployeeWithdrawalRequest> Entries,
    DateOnly? DefaultTransactionDate = null,
    int? DefaultCashboxId = null,
    int? DefaultCashMovementTypeId = null);

/// <summary>
/// Request to update an existing manual employee transaction.
/// </summary>
public sealed record EmployeeTransactionUpdateRequest(
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes = null);
