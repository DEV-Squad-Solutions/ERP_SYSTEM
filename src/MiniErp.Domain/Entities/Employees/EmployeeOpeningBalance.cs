using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Employees;

public sealed class EmployeeOpeningBalance : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int? PayrollEntryId { get; set; }
    public PayrollEntry? PayrollEntry { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public int? ExchangeRateId { get; private set; }
    public ExchangeRate? ExchangeRateRecord { get; private set; }

    public decimal ExchangeRate { get; private set; } = 1m;

    public EmployeeBalanceType BalanceType { get; set; }

    public decimal Amount { get; set; }

    public decimal BaseAmount { get; private set; }

    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public void ApplyExchangeRate(
        int? exchangeRateId,
        decimal exchangeRate)
    {
        ExchangeRateId = exchangeRateId;
        ExchangeRate = ExchangeRateRules.RoundRate(exchangeRate);
        BaseAmount = ExchangeRateRules.ConvertToBase(
            Amount,
            ExchangeRate);
    }
}
