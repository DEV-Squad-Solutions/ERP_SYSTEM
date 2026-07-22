using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
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

    public InvoiceType InvoiceType { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    // References the original sale or purchase when this invoice is a return.
    // It is null for normal sales and purchase invoices.
    public int? InvoiceId { get; set; }

    // Navigation to the original invoice referenced by InvoiceId.
    public Invoice? OriginalInvoice { get; set; }

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int? ContainerStoreId { get; set; }

    public Store? ContainerStore { get; set; }

    public int? CountryId { get; set; }

    public Country? Country { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public bool IsExternalDriver { get; set; }
    public string? ExternalDriverName { get; set; }
    public string? VehicleNumber { get; set; }

    public decimal Total { get; private set; }

    public string? Notes { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];

    public ICollection<InvoiceContainerLine> ContainerLines { get; set; } = [];

    public void CalculateTotal()
    {
        foreach (var line in Lines)
        {
            line.CalculateAmounts();
        }

        Total = Lines.Sum(line => line.Total);
    }

    public PaymentStatus GetPaymentStatus(decimal paidAmount)
    {
        if (paidAmount <= 0)
        {
            return PaymentStatus.Unpaid;
        }

        return paidAmount < Total
            ? PaymentStatus.PartiallyPaid
            : PaymentStatus.Paid;
    }
}
