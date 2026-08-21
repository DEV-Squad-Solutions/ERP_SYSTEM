namespace MiniErp.Application.Features.PayrollEntries
{

    /// <summary>
    /// Confirms a calculated payroll entry, posting the net salary as a Credit
    /// to the employee's account ledger. The employee can then withdraw cash
    /// using the EmployeeTransactions withdrawal endpoint.
    /// </summary>
    public sealed record PayrollEntrySalaryPaymentRequest(
        DateOnly PostingDate,
        string? Notes = null);
}