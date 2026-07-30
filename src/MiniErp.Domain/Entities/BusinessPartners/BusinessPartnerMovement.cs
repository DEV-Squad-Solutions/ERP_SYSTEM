using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.BusinessPartners;

public sealed class BusinessPartnerMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public int? CashVoucherId { get; set; }

    public CashVoucher? CashVoucher { get; set; }

    public BusinessPartnerMovementType MovementType { get; set; }

    public DateOnly MovementDate { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal ExchangeRate { get; private set; } = 1m;

    public decimal BaseDebit { get; private set; }

    public decimal BaseCredit { get; private set; }

    public string? Description { get; set; }

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
