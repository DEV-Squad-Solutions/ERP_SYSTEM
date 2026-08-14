namespace MiniErp.Domain.Enums;

public enum EmployeeTransactionSource
{
    /// <summary>Manually entered by an admin (bonus, deduction, etc.).</summary>
    Manual = 1,

    /// <summary>Automatically posted when a PayrollEntry salary is confirmed.</summary>
    Payroll = 2
}
