using Mapster;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed class CashboxMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashboxRequest, Cashbox>()
            .Map(cashbox => cashbox.Code, request => request.Code.Trim())
            .Map(cashbox => cashbox.Name, request => request.Name.Trim())
            .Map(cashbox => cashbox.Notes, request => Normalize(request.Notes));

        config.ForType<CashboxUpdateRequest, Cashbox>()
            .Ignore(cashbox => cashbox.RowVersion)
            .Map(cashbox => cashbox.Code, request => request.Code.Trim())
            .Map(cashbox => cashbox.Name, request => request.Name.Trim())
            .Map(cashbox => cashbox.Notes, request => Normalize(request.Notes));

        config.ForType<Cashbox, CashboxResponse>()
            .Map(
                response => response.CurrentBalance,
                cashbox => cashbox.OpeningBalance +
                    cashbox.Vouchers.Sum(voucher =>
                        voucher.Direction == CashDirection.Receipt
                            ? voucher.Amount
                            : -voucher.Amount));

        config.ForType<Cashbox, CashboxSelectResponse>()
            .Map(
                response => response.CurrentBalance,
                cashbox => cashbox.OpeningBalance +
                    cashbox.Vouchers.Sum(voucher =>
                        voucher.Direction == CashDirection.Receipt
                            ? voucher.Amount
                            : -voucher.Amount));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
