using Mapster;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashboxTransfers;

public sealed class CashboxTransferMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashboxTransfer, CashboxTransferListResponse>()
            .Map(
                response => response.SourceCashboxName,
                transfer => transfer.SourceCashbox.Name)
            .Map(
                response => response.DestinationCashboxName,
                transfer => transfer.DestinationCashbox.Name)
            .Map(
                response => response.Amount,
                transfer => transfer.Vouchers
                    .Where(voucher =>
                        voucher.Direction == CashDirection.Payment)
                    .Select(voucher => voucher.Amount)
                    .FirstOrDefault())
            .Map(
                response => response.Currency,
                transfer => transfer.SourceCashbox.Currency);
    }
}
