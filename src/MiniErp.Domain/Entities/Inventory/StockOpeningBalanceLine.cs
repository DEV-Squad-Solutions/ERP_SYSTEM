using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class StockOpeningBalanceLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StockOpeningBalanceId { get; set; }

    public StockOpeningBalance StockOpeningBalance { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int? ItemUnitId { get; set; }

    public ItemUnit? ItemUnit { get; set; }

    public int Count { get; set; }

    public decimal Weight { get; set; }

    public decimal Quantity { get; private set; }

    public decimal Price { get; set; }

    public decimal Total { get; private set; }

    public string? Notes { get; set; }

    public void CalculateAmounts()
    {
        if (!StockOpeningBalanceAmountRules.TryCalculate(
                Count,
                Weight,
                Price,
                out var quantity,
                out var total))
        {
            throw new InvalidOperationException(
                "The opening-balance line values cannot be represented by the configured quantity and money precision.");
        }

        Quantity = quantity;
        Total = total;
    }
}
