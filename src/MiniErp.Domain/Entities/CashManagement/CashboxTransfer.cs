using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.CashManagement;

public sealed class CashboxTransfer : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string TransferNumber { get; set; } = string.Empty;

    public DateOnly TransferDate { get; set; }

    public int SourceCashboxId { get; set; }

    public Cashbox SourceCashbox { get; set; } = null!;

    public int DestinationCashboxId { get; set; }

    public Cashbox DestinationCashbox { get; set; } = null!;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CashVoucher> Vouchers { get; set; } = [];

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }
}
