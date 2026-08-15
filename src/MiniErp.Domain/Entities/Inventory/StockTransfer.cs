using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class StockTransfer : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly TransferDate { get; set; }

    public int SourceStoreId { get; set; }

    public Store SourceStore { get; set; } = null!;

    public int DestinationStoreId { get; set; }

    public Store DestinationStore { get; set; } = null!;

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<StockTransferLine> Lines { get; set; } = [];

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }
}
