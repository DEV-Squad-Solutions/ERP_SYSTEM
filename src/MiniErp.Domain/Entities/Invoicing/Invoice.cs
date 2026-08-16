using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Invoicing;

public sealed class Invoice : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string InvoiceNumber { get; set; } = string.Empty;

    public string? ExportInvoiceCode { get; set; }

    public string? PartnerInvoiceNo { get; set; }

    public InvoiceType InvoiceType { get; set; }

    public InvoiceContentType ContentType { get; set; } =
        InvoiceContentType.Items;

    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Cash;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int? ContainerStoreId { get; set; }

    public Store? ContainerStore { get; set; }

    public int? CountryId { get; set; }

    public Country? Country { get; set; }

    public int? ItemsCategoryId { get; set; }

    public ItemsCategory? ItemsCategory { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public int? ExchangeRateId { get; private set; }

    public ExchangeRate? ExchangeRateRecord { get; private set; }

    public decimal ExchangeRate { get; private set; } = 1m;

    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public string? ActualDriverId { get; set; }

    public bool UsesExternalDriver { get; set; }
    public string? ExternalDriverName { get; set; }
    public string? VehicleNumber { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal WBWeight { get; set; }

    public decimal WBScaleDifference { get; set; }

    public decimal WBDiscount { get; set; }

    public decimal WBTotal { get; private set; }

    public decimal PaidAmount { get; set; }

    public decimal Total { get; private set; }

    public decimal BaseSubtotal { get; private set; }

    public decimal BaseDiscountAmount { get; private set; }

    public decimal BaseTotal { get; private set; }

    public decimal BasePaidAmountAtInvoiceRate { get; private set; }

    public decimal Subtotal =>
        Lines.Sum(line => line.Total);

    public decimal RemainingAmount =>
        Total - PaidAmount;

    public PaymentStatus PaymentStatus =>
        PaidAmount <= 0m && Total > 0m
            ? PaymentStatus.Unpaid
            : RemainingAmount <= 0m
                ? PaymentStatus.Paid
                : PaymentStatus.PartiallyPaid;

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<InvoiceLine> Lines { get; set; } = [];

    public ICollection<InvoiceContainerLine> ContainerLines { get; set; } = [];

    public ICollection<CashVoucher> PaymentVouchers { get; set; } = [];

    public ICollection<InvoicePayment> Payments { get; set; } = [];

    public void CalculateTotal()
    {
        foreach (var line in Lines)
        {
            line.CalculateAmounts();
        }

        Total = decimal.Round(
            Subtotal - DiscountAmount,
            InvoiceAmountRules.MoneyScale,
            MidpointRounding.AwayFromZero);

        WBTotal = decimal.Round(
            WBWeight - WBScaleDifference - WBDiscount,
            InvoiceAmountRules.QuantityScale,
            MidpointRounding.AwayFromZero);
    }

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }

    public PaymentStatus GetPaymentStatus() => PaymentStatus;

    public void ApplyExchangeRate(
        int? exchangeRateId,
        decimal exchangeRate)
    {
        if (!ExchangeRateRules.IsValidRate(exchangeRate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(exchangeRate),
                "Exchange rate must be greater than zero.");
        }

        ExchangeRateId = exchangeRateId;
        ExchangeRate = ExchangeRateRules.RoundRate(exchangeRate);

        foreach (var line in Lines)
        {
            line.ApplyExchangeRate(ExchangeRate);
        }

        BaseSubtotal = ExchangeRateRules.ConvertToBase(
            Subtotal,
            ExchangeRate);
        BaseDiscountAmount = ExchangeRateRules.ConvertToBase(
            DiscountAmount,
            ExchangeRate);
        BaseTotal = ExchangeRateRules.ConvertToBase(
            Total,
            ExchangeRate);
        BasePaidAmountAtInvoiceRate = ExchangeRateRules.ConvertToBase(
            PaidAmount,
            ExchangeRate);
    }
}
