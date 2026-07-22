using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities;

public sealed class BusinessPartnerVoucher : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public string VoucherNumber { get; set; } = string.Empty;

    public VoucherType VoucherType { get; set; }

    public DateOnly VoucherDate { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal Amount { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? Notes { get; set; }

    public ICollection<BusinessPartnerVoucherAllocation> Allocations { get; set; } = [];

    // A posted voucher creates one financial movement for its full amount.
    public ICollection<BusinessPartnerMovement> Movements { get; set; } = [];

    public decimal GetAllocatedAmount() =>
        Allocations.Sum(allocation => allocation.Amount);

    public decimal GetUnallocatedAmount() =>
        Amount - GetAllocatedAmount();
}
