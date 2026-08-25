using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public interface IEmployeeTransactionService
{
    Task<Result<PagedResponse<EmployeeTransactionResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeTransactionFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAccountBalanceResponse>> GetBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Post a manual Credit, Debit, Bonus, or Deduction to the employee account.
    /// Automatically generates a CashVoucher via CashVoucherService with the specified Cashbox.
    /// </summary>
    Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Post multiple ledger entries to employee accounts in bulk with cash vouchers.
    /// </summary>
    Task<Result<List<EmployeeTransactionResponse>>> AddBulkAsync(
        BulkEmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraw cash from the employee account: debits account + creates a CashVoucher payment via CashVoucherService.
    /// Also used for Advance payments.
    /// </summary>
    Task<Result<EmployeeTransactionResponse>> WithdrawAsync(
        EmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process bulk cash withdrawals or advances for multiple employees.
    /// </summary>
    Task<Result<List<EmployeeTransactionResponse>>> WithdrawBulkAsync(
        BulkEmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called internally by PayrollEntryService when a salary is confirmed.
    /// Credits the employee account with the net salary amount and generates a backing CashVoucher.
    /// </summary>
    Task<Result<EmployeeTransactionResponse>> PostSalaryCreditAsync(
        int employeeId,
        decimal amount,
        int payrollEntryId,
        DateOnly transactionDate,
        int cashboxId,
        int cashMovementTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called internally by PayrollEntryService when salaries are confirmed in bulk.
    /// Credits each employee's account with their net salary amount and creates backing CashVouchers.
    /// </summary>
    Task<Result<List<EmployeeTransactionResponse>>> PostSalaryCreditBulkAsync(
        IReadOnlyList<EmployeeSalaryCreditItem> items,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeTransactionUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an account statement report for an employee over a specified date range.
    /// </summary>
    Task<Result<EmployeeStatementResponse>> GetStatementAsync(
        int employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}

public sealed record EmployeeSalaryCreditItem(
    int EmployeeId,
    decimal Amount,
    int PayrollEntryId,
    DateOnly TransactionDate,
    int CashboxId,
    int CashMovementTypeId,
    string? Notes = null);
