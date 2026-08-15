using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Domain.Entities.Catalog;

public sealed class ItemsCategory : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<Invoice> Invoices { get; set; } = [];
}
