using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Domain.Entities.BusinessPartners;

public sealed class BusinessPartnerVoucherAllocation : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerVoucherId { get; set; }

    public BusinessPartnerVoucher BusinessPartnerVoucher { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }
}
