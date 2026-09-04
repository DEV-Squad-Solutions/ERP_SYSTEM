using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Employees;

public static class EmployeeAccountRules
{
    // ── Movement Classification ──────────────────────────────────────────────
    public static bool IsCreditMovement(EmployeeMovementType type) =>
        type is EmployeeMovementType.Credit or EmployeeMovementType.Bonus;

    // ── Debit / Credit Split ─────────────────────────────────────────────────
    public static (decimal Debit, decimal Credit) SplitAmount(
        EmployeeMovementType type,
        decimal amount) =>
        IsCreditMovement(type)
            ? (0m, amount)
            : (amount, 0m);

    // ── Signed Amount ────────────────────────────────────────────────────────

    public static decimal SignedAmount(decimal debit, decimal credit) =>
        credit - debit;

    // ── Balance Calculation ──────────────────────────────────────────────────
    public static decimal CalculateBalance(
        decimal totalCredits,
        decimal totalDebits) =>
        totalCredits - totalDebits;

    // ── Display Helpers ──────────────────────────────────────────────────────
    public static string GetBalanceDescription(decimal netBalance) =>
        netBalance > 0
            ? "دائن (مستحق للموظف)"
            : netBalance < 0
                ? "مدين (مستحق على الموظف)"
                : "متزن (صفر)";

    public static string GetMovementTypeName(EmployeeMovementType type) =>
        type switch
        {
            EmployeeMovementType.Credit     => "حركة دائنة",
            EmployeeMovementType.Debit      => "حركة مدينة",
            EmployeeMovementType.Advance    => "سلفة نقدية",
            EmployeeMovementType.Deduction  => "خصم مالي",
            EmployeeMovementType.Bonus      => "مكافأة مالية",
            EmployeeMovementType.Withdrawal => "مسحوبات نقدية",
            _                               => "حركة حساب موظف"
        };
}

