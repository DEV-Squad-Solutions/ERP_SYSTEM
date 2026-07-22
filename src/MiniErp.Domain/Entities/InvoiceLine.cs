using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class InvoiceLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int ItemUnitId { get; set; }

    public ItemUnit ItemUnit { get; set; } = null!;

    public int Count { get; set; }

    public decimal Weight { get; set; }

    public decimal Quantity { get; private set; }

    public decimal Price { get; set; }

    public decimal Total { get; private set; }

    public string? Notes { get; set; }

    public void CalculateAmounts()
    {
        Quantity = Count * Weight;
        Total = Quantity * Price;
    }
}
