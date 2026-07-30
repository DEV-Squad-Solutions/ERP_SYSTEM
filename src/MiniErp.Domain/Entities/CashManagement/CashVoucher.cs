using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.CashManagement;

public sealed class CashVoucher : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public string VoucherNumber { get; set; } = string.Empty;

    public DateOnly VoucherDate { get; set; }

    public CashDirection Direction { get; set; }

    public int CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    public int CashMovementTypeId { get; set; }

    public CashMovementType CashMovementType { get; set; } = null!;

    public CashPartyType PartyType { get; set; }

    public int? BusinessPartnerId { get; set; }

    public BusinessPartner? BusinessPartner { get; set; }

    public int? DriverId { get; set; }

    public Driver? Driver { get; set; }

    public int? DriverTripId { get; set; }

    public DriverTrip? DriverTrip { get; set; }

    public string? ExternalPartyName { get; set; }

    public decimal Amount { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public int? ExchangeRateId { get; private set; }

    public ExchangeRate? ExchangeRateRecord { get; private set; }

    public decimal ExchangeRate { get; private set; } = 1m;

    public decimal BaseAmount { get; private set; }

    public string? ReferenceNumber { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public InvoicePayment? InvoicePayment { get; set; }

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }

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
        BaseAmount = ExchangeRateRules.ConvertToBase(
            Amount,
            ExchangeRate);
    }
}
