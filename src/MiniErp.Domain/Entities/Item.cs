using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class Item : AuditableEntity
{
    public int Id { get; set; }

    public int ItemUnitId { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ItemUnit ItemUnit { get; set; } = null!;
}
