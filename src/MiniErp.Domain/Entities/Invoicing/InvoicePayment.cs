using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Invoicing;

public sealed class InvoicePayment : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int CashVoucherId { get; set; }

    public CashVoucher CashVoucher { get; set; } = null!;

    public CurrencyCode InvoiceCurrency { get; private set; }

    public decimal AppliedAmount { get; private set; }

    public CurrencyCode CashboxCurrency { get; private set; }

    public decimal CashboxAmount { get; private set; }

    public decimal InvoiceToBaseRate { get; private set; }

    public decimal CashboxToBaseRate { get; private set; }

    public decimal AppliedBaseAmount { get; private set; }

    public decimal CashboxBaseAmount { get; private set; }

    public decimal RealizedExchangeDifference { get; private set; }

    public void Apply(
        CurrencyCode invoiceCurrency,
        decimal appliedAmount,
        CurrencyCode cashboxCurrency,
        decimal cashboxAmount,
        decimal invoiceToBaseRate,
        decimal cashboxToBaseRate)
    {
        InvoiceCurrency = invoiceCurrency;
        AppliedAmount = decimal.Round(
            appliedAmount,
            InvoiceAmountRules.MoneyScale,
            MidpointRounding.AwayFromZero);
        CashboxCurrency = cashboxCurrency;
        CashboxAmount = decimal.Round(
            cashboxAmount,
            InvoiceAmountRules.MoneyScale,
            MidpointRounding.AwayFromZero);
        InvoiceToBaseRate =
            ExchangeRateRules.RoundRate(invoiceToBaseRate);
        CashboxToBaseRate =
            ExchangeRateRules.RoundRate(cashboxToBaseRate);
        AppliedBaseAmount = ExchangeRateRules.ConvertToBase(
            AppliedAmount,
            InvoiceToBaseRate);
        CashboxBaseAmount = ExchangeRateRules.ConvertToBase(
            CashboxAmount,
            CashboxToBaseRate);
        RealizedExchangeDifference =
            ExchangeRateRules.RoundBaseAmount(
                CashboxBaseAmount - AppliedBaseAmount);
    }
}
