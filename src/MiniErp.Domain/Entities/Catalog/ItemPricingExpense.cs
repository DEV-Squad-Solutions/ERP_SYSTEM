using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Catalog;

/// <summary>
/// An advisory per-unit expense used only when presenting an indicative item cost.
/// It does not participate in inventory costing or accounting posting.
/// </summary>
public sealed class ItemPricingExpense : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Advisory expense amount per item unit.</summary>
    public decimal Amount { get; set; }

    public string? Notes { get; set; }
}
