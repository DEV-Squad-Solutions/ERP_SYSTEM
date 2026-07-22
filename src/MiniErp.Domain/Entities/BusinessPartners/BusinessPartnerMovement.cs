using MiniErp.Domain.Common.Entities;
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

    // Identifies the voucher that created this movement, when applicable.
    public int? BusinessPartnerVoucherId { get; set; }

    public BusinessPartnerVoucher? BusinessPartnerVoucher { get; set; }

    // Identifies an invoice-originated movement. Voucher-to-invoice settlement
    // is tracked separately by BusinessPartnerVoucherAllocation rows.
    public int? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public BusinessPartnerMovementType MovementType { get; set; }

    public int ReferenceId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateOnly MovementDate { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public string? Description { get; set; }

}
