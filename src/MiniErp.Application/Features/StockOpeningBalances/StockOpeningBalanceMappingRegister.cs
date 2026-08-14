using Mapster;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed class StockOpeningBalanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<StockOpeningBalanceLineRequest, StockOpeningBalanceLine>()
            .Map(line => line.Notes, request => Normalize(request.Notes));

        config.ForType<StockOpeningBalanceRequest, StockOpeningBalance>()
            .Ignore(balance => balance.Lines)
            .Ignore(balance => balance.DocumentNumber)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<StockOpeningBalanceUpdateRequest, StockOpeningBalance>()
            .Ignore(balance => balance.Lines)
            .Ignore(balance => balance.RowVersion)
            .Ignore(balance => balance.DocumentNumber)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<StockOpeningBalanceLine, StockOpeningBalanceLineResponse>()
            .Map(response => response.ItemCode, line => line.Item.Code)
            .Map(response => response.ItemName, line => line.Item.Name)
            .Map(
                response => response.ItemUnitName,
                line => line.ItemUnit == null ? null : line.ItemUnit.Name);

        config.ForType<StockOpeningBalance, StockOpeningBalanceListResponse>()
            .Map(response => response.StoreName, balance => balance.Store.Name)
            .Map(response => response.LineCount, balance => balance.Lines.Count())
            .Map(
                response => response.Lines,
                balance => balance.Lines.OrderBy(line => line.Id));

        config.ForType<StockOpeningBalance, StockOpeningBalanceResponse>()
            .Map(response => response.StoreName, balance => balance.Store.Name)
            .Map(
                response => response.Lines,
                balance => balance.Lines.OrderBy(line => line.Id));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
