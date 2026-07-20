using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class ItemUnit : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;

    public ICollection<Item> Items { get; set; } = [];
}
