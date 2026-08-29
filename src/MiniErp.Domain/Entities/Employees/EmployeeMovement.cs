using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Employees;

public sealed class EmployeeMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int? CashVoucherId { get; set; }
    public CashVoucher? CashVoucher { get; set; }

    public EmployeeMovementType Type { get; set; }

    public DateOnly MovementDate { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal ExchangeRate { get; private set; } = 1m;
    public decimal BaseDebit { get; private set; }
    public decimal BaseCredit { get; private set; }
    public string? Notes { get; set; }

    public void ApplyAmounts(EmployeeMovementType type, decimal amount)
    {
        Type = type;
        var (debit, credit) = EmployeeAccountRules.SplitAmount(type, amount);
        Debit = debit;
        Credit = credit;
    }

    public void ApplyExchangeRate(decimal exchangeRate)
    {
        if (!ExchangeRateRules.IsValidRate(exchangeRate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(exchangeRate),
                "Exchange rate must be greater than zero.");
        }

        ExchangeRate = ExchangeRateRules.RoundRate(exchangeRate);
        BaseDebit = ExchangeRateRules.ConvertToBase(
            Debit,
            ExchangeRate);
        BaseCredit = ExchangeRateRules.ConvertToBase(
            Credit,
            ExchangeRate);
    }
}
