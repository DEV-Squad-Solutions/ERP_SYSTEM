namespace MiniErp.Domain.Enums;

/// <summary>
/// Describes the balance direction of a Business Partner or Employee account,
/// used as a list-filter parameter.
/// </summary>
/// <remarks>
/// Business Partner balance sign convention: Balance = SUM(Debit) − SUM(Credit).
///   Positive → partner owes the company (Debit Balance).
///   Negative → company owes the partner (Credit Balance).
///
/// Employee balance sign convention: Balance = SUM(Credit) − SUM(Debit).
///   Positive → company owes the employee (Credit Balance).
///   Negative → employee owes the company (Debit Balance).
/// </remarks>
public enum BalanceStatus
{
    /// <summary>No balance filter — return all records regardless of balance.</summary>
    All = 0,

    /// <summary>
    /// Business Partner: the company owes the partner (SUM Debit − SUM Credit &lt; 0).
    /// Employee: the company owes the employee (SUM Credit − SUM Debit &gt; 0).
    /// </summary>
    CreditBalance = 1,

    /// <summary>
    /// Business Partner: the partner owes the company (SUM Debit − SUM Credit &gt; 0).
    /// Employee: the employee owes the company (SUM Credit − SUM Debit &lt; 0).
    /// </summary>
    DebitBalance = 2,

    /// <summary>No outstanding balance (net balance equals zero or no transactions exist).</summary>
    ZeroBalance = 3,
}
