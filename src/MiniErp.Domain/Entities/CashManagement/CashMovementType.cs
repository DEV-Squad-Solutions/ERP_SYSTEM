using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.CashManagement;

public sealed class CashMovementType : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public CashDirection Direction { get; set; }

    public CashMovementClassification Classification { get; set; }

    public PartnerAccountEffect PartnerEffect { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefaultForSales { get; set; }

    public bool IsDefaultForPurchase { get; set; }

    public bool IsDefaultForSalesReturn { get; set; }

    public bool IsDefaultForPurchaseReturn { get; set; }

    public string? Notes { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CashVoucher> Vouchers { get; set; } = [];
}
