using Mapster;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed class CashboxMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashboxRequest, Cashbox>()
            .Ignore(cashbox => cashbox.OpeningBalanceDate)
            .Ignore(cashbox => cashbox.OpeningExchangeRateId)
            .Ignore(cashbox => cashbox.OpeningExchangeRateRecord)
            .Ignore(cashbox => cashbox.OpeningExchangeRate)
            .Ignore(cashbox => cashbox.BaseOpeningBalance)
            .Map(cashbox => cashbox.Code, request => request.Code.Trim())
            .Map(cashbox => cashbox.Name, request => request.Name.Trim())
            .Map(cashbox => cashbox.Notes, request => Normalize(request.Notes));

        config.ForType<CashboxUpdateRequest, Cashbox>()
            .Ignore(cashbox => cashbox.RowVersion)
            .Ignore(cashbox => cashbox.OpeningBalanceDate)
            .Ignore(cashbox => cashbox.OpeningExchangeRateId)
            .Ignore(cashbox => cashbox.OpeningExchangeRateRecord)
            .Ignore(cashbox => cashbox.OpeningExchangeRate)
            .Ignore(cashbox => cashbox.BaseOpeningBalance)
            .Map(cashbox => cashbox.Code, request => request.Code.Trim())
            .Map(cashbox => cashbox.Name, request => request.Name.Trim())
            .Map(cashbox => cashbox.Notes, request => Normalize(request.Notes));

        config.ForType<Cashbox, CashboxResponse>()
            .Map(
                response => response.BaseCurrency,
                cashbox => cashbox.Company.Settings == null
                    ? CurrencyCode.EGP
                    : cashbox.Company.Settings.BaseCurrency)
            .Map(
                response => response.CurrentBalance,
                cashbox => cashbox.OpeningBalance +
                    cashbox.Vouchers
                        .Where(voucher =>
                            voucher.CashMovementTypeId.HasValue)
                        .Sum(voucher =>
                            voucher.Direction == CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount));

        config.ForType<Cashbox, CashboxSelectResponse>()
            .Map(
                response => response.CurrentBalance,
                cashbox => cashbox.OpeningBalance +
                    cashbox.Vouchers
                        .Where(voucher =>
                            voucher.CashMovementTypeId.HasValue)
                        .Sum(voucher =>
                            voucher.Direction == CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
