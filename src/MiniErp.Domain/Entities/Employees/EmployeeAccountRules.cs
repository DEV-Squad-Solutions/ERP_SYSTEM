using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Employees;

public static class EmployeeAccountRules
{
    public static bool IsCreditMovement(EmployeeMovementType type) =>
        type is EmployeeMovementType.Credit or EmployeeMovementType.Bonus;

    public static bool RequiresCashVoucher(EmployeeMovementType type) =>
        type is EmployeeMovementType.Advance or EmployeeMovementType.Withdrawal;

    public static (decimal Debit, decimal Credit) SplitAmount(
        EmployeeMovementType type,
        decimal amount) =>
        IsCreditMovement(type)
            ? (0m, amount)
            : (amount, 0m);

    public static (decimal Debit, decimal Credit) SplitAmount(
        EmployeeBalanceType balanceType,
        decimal amount) =>
        balanceType == EmployeeBalanceType.Credit
            ? (0m, amount)
            : (amount, 0m);

    public static decimal SignedAmount(
        EmployeeBalanceType balanceType,
        decimal amount) =>
        balanceType == EmployeeBalanceType.Credit
            ? amount
            : -amount;

    public static decimal SignedAmount(decimal debit, decimal credit) =>
        credit - debit;
}
