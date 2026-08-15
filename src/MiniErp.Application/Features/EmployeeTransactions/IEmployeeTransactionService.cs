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

    /// <summary>Post a manual Credit, Debit, Bonus, or Deduction to the employee account.</summary>
    Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraw cash from the employee account: debits account + creates a CashVoucher payment.
    /// Also used for Advance payments.
    /// </summary>
    Task<Result<EmployeeTransactionResponse>> WithdrawAsync(
        EmployeeWithdrawalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called internally by PayrollEntryService when a salary is confirmed.
    /// Credits the employee account with the net salary amount.
    /// </summary>
    Task<Result<EmployeeTransactionResponse>> PostSalaryCreditAsync(
        int employeeId,
        decimal amount,
        int payrollEntryId,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
