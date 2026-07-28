using Mapster;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed class StockAdjustmentMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<StockAdjustmentRequest, StockAdjustment>()
            .Ignore(adjustment => adjustment.Lines)
            .Map(
                adjustment => adjustment.DocumentNumber,
                request => request.DocumentNumber.Trim())
            .Map(
                adjustment => adjustment.Reason,
                request => Normalize(request.Reason));

        config.ForType<StockAdjustmentUpdateRequest, StockAdjustment>()
            .Ignore(adjustment => adjustment.Lines)
            .Ignore(adjustment => adjustment.RowVersion)
            .Map(
                adjustment => adjustment.DocumentNumber,
                request => request.DocumentNumber.Trim())
            .Map(
                adjustment => adjustment.Reason,
                request => Normalize(request.Reason));

        config.ForType<StockAdjustmentLineRequest, StockAdjustmentLine>()
            .Map(
                line => line.Reason,
                request => Normalize(request.Reason));

        config.ForType<StockAdjustmentLine, StockAdjustmentLineResponse>()
            .Map(response => response.ItemCode, line => line.Item.Code)
            .Map(response => response.ItemName, line => line.Item.Name)
            .Map(response => response.ItemUnitName, line => line.ItemUnit.Name);

        config.ForType<StockAdjustment, StockAdjustmentListResponse>()
            .Map(response => response.StoreName, item => item.Store.Name)
            .Map(response => response.LineCount, item => item.Lines.Count())
            .Map(
                response => response.Lines,
                item => item.Lines
                    .OrderBy(line => line.Item.Name)
                    .ThenBy(line => line.ItemId)
                    .ThenBy(line => line.Id));

        config.ForType<StockAdjustment, StockAdjustmentResponse>()
            .Map(response => response.StoreName, item => item.Store.Name)
            .Map(
                response => response.Lines,
                item => item.Lines
                    .OrderBy(line => line.Item.Name)
                    .ThenBy(line => line.ItemId)
                    .ThenBy(line => line.Id));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
