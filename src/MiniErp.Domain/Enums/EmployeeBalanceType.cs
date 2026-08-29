namespace MiniErp.Domain.Enums;

/// <summary>
/// Employee account side, matching partner opening-balance semantics:
/// Debit = Receivable analog (employee owes the company);
/// Credit = Payable analog (company owes the employee).
/// </summary>
public enum EmployeeBalanceType
{
    Debit = 1,
    Credit = 2
}
