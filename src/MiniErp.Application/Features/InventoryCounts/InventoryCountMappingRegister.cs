using Mapster;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InventoryCounts;

public sealed class InventoryCountMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<InventoryCountRequest, InventoryCount>()
            .Map(
                count => count.DocumentNumber,
                request => request.DocumentNumber.Trim())
            .Map(
                count => count.Notes,
                request => Normalize(request.Notes));

        config.ForType<InventoryCountLine, InventoryCountLineResponse>()
            .Map(response => response.ItemCode, line => line.Item.Code)
            .Map(response => response.ItemName, line => line.Item.Name)
            .Map(response => response.ItemUnitName, line => line.ItemUnit.Name)
            .Map(
                response => response.Difference,
                line => line.PhysicalQuantity.HasValue
                    ? line.PhysicalQuantity.Value - line.SystemQuantity
                    : (decimal?)null);

        config.ForType<InventoryCount, InventoryCountListResponse>()
            .Map(response => response.StoreName, count => count.Store.Name)
            .Map(response => response.LineCount, count => count.Lines.Count())
            .Map(
                response => response.CountedLineCount,
                count => count.Lines.Count(line =>
                    line.PhysicalQuantity.HasValue))
            .Map(
                response => response.DifferenceLineCount,
                count => count.Lines.Count(line =>
                    line.PhysicalQuantity.HasValue &&
                    line.PhysicalQuantity.Value != line.SystemQuantity))
            .Map(
                response => response.IncreaseAdjustmentId,
                count => count.GeneratedStockAdjustments
                    .Where(adjustment =>
                        adjustment.Direction ==
                        StockAdjustmentDirection.Increase)
                    .Select(adjustment => (int?)adjustment.Id)
                    .FirstOrDefault())
            .Map(
                response => response.DecreaseAdjustmentId,
                count => count.GeneratedStockAdjustments
                    .Where(adjustment =>
                        adjustment.Direction ==
                        StockAdjustmentDirection.Decrease)
                    .Select(adjustment => (int?)adjustment.Id)
                    .FirstOrDefault());

        config.ForType<InventoryCount, InventoryCountResponse>()
            .Map(response => response.StoreName, count => count.Store.Name)
            .Map(
                response => response.IncreaseAdjustmentId,
                count => count.GeneratedStockAdjustments
                    .Where(adjustment =>
                        adjustment.Direction ==
                        StockAdjustmentDirection.Increase)
                    .Select(adjustment => (int?)adjustment.Id)
                    .FirstOrDefault())
            .Map(
                response => response.DecreaseAdjustmentId,
                count => count.GeneratedStockAdjustments
                    .Where(adjustment =>
                        adjustment.Direction ==
                        StockAdjustmentDirection.Decrease)
                    .Select(adjustment => (int?)adjustment.Id)
                    .FirstOrDefault())
            .Map(
                response => response.Lines,
                count => count.Lines
                    .OrderBy(line => line.Item.Name)
                    .ThenBy(line => line.ItemId)
                    .ThenBy(line => line.Id));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
