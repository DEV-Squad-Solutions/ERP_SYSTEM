namespace MiniErp.Domain.Enums;

public enum EmployeeMovementType
{
    /// <summary>Reduces the balance (company owes less — employee takes money).</summary>
    Debit = 1,

    /// <summary>Increases the balance (company owes more — e.g. salary posted).</summary>
    Credit = 2,

    /// <summary>Cash advance — debits account AND creates a CashVoucher payment.</summary>
    Advance = 3,

    /// <summary>Manual monetary deduction from the employee's account balance.</summary>
    Deduction = 4,

    /// <summary>Manual bonus credit added to the employee's account balance.</summary>
    Bonus = 5,

    /// <summary>Cash withdrawal — debits account AND creates a CashVoucher payment.</summary>
    Withdrawal = 6
}
