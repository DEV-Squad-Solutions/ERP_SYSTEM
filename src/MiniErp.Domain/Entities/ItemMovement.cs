using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities;

public sealed class ItemMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int ItemUnitId { get; set; }

    public ItemUnit ItemUnit { get; set; } = null!;

    public ItemMovementType MovementType { get; set; }

    public int ReferenceId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateOnly MovementDate { get; set; }

    public decimal QuantityIn { get; set; }

    public decimal QuantityOut { get; set; }

    public string? Description { get; set; }

}
