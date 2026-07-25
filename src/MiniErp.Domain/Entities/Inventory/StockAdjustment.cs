using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class StockAdjustment : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public StockAdjustmentDirection Direction { get; set; }

    public string? Reason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<StockAdjustmentLine> Lines { get; set; } = [];
}
