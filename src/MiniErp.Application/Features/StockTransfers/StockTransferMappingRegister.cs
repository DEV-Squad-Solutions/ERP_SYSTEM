using Mapster;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockTransfers;

public sealed class StockTransferMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<StockTransferRequest, StockTransfer>()
            .Ignore(transfer => transfer.Lines)
            .Ignore(transfer => transfer.DocumentNumber)
            .Map(
                transfer => transfer.Notes,
                request => Normalize(request.Notes));

        config.ForType<StockTransferUpdateRequest, StockTransfer>()
            .Ignore(transfer => transfer.Lines)
            .Ignore(transfer => transfer.RowVersion)
            .Map(
                transfer => transfer.Notes,
                request => Normalize(request.Notes));

        config.ForType<StockTransferLineRequest, StockTransferLine>()
            .Map(
                line => line.Notes,
                request => Normalize(request.Notes));

        config.ForType<StockTransfer, StockTransferListResponse>()
            .Map(response => response.SourceStoreName, transfer => transfer.SourceStore.Name)
            .Map(response => response.DestinationStoreName, transfer => transfer.DestinationStore.Name)
            .Map(response => response.LineCount, transfer => transfer.Lines.Count())
            .Map(response => response.TotalQuantity, transfer => transfer.Lines.Sum(line => line.Quantity));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
