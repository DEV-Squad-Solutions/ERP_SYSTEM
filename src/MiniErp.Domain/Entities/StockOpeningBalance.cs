using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities;

public sealed class StockOpeningBalance : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? Notes { get; set; }

    public ICollection<StockOpeningBalanceLine> Lines { get; set; } = [];
}
