using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class ItemUnit : AuditableEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Item> Items { get; set; } = [];
}
