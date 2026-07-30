using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.CashManagement;

public sealed class Cashbox : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal OpeningBalance { get; set; }

    public DateOnly OpeningBalanceDate { get; private set; }

    public int? OpeningExchangeRateId { get; private set; }

    public ExchangeRate? OpeningExchangeRateRecord { get; private set; }

    public decimal OpeningExchangeRate { get; private set; } = 1m;

    public decimal BaseOpeningBalance { get; private set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CashVoucher> Vouchers { get; set; } = [];

    public void ApplyOpeningExchangeRate(
        DateOnly openingBalanceDate,
        int? exchangeRateId,
        decimal exchangeRate)
    {
        OpeningBalanceDate = openingBalanceDate;
        OpeningExchangeRateId = exchangeRateId;
        OpeningExchangeRate =
            ExchangeRateRules.RoundRate(exchangeRate);
        BaseOpeningBalance = ExchangeRateRules.ConvertToBase(
            OpeningBalance,
            OpeningExchangeRate);
    }
}
