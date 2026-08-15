using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class InventoryCount : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly CountDate { get; set; }

    public DateTime SnapshotTakenAt { get; set; }

    public DateTime? ReconciledAt { get; set; }

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<InventoryCountLine> Lines { get; set; } = [];

    public ICollection<StockAdjustment> GeneratedStockAdjustments { get; set; } = [];

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }
}
