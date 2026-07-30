using Mapster;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashVoucherRequest, CashVoucher>()
            .Map(
                voucher => voucher.VoucherNumber,
                request => request.VoucherNumber.Trim())
            .Map(
                voucher => voucher.ExternalPartyName,
                request => Normalize(request.ExternalPartyName))
            .Map(
                voucher => voucher.ReferenceNumber,
                request => Normalize(request.ReferenceNumber))
            .Map(
                voucher => voucher.Description,
                request => Normalize(request.Description))
            .Map(voucher => voucher.Notes, request => Normalize(request.Notes));

        config.ForType<CashVoucherUpdateRequest, CashVoucher>()
            .Ignore(voucher => voucher.RowVersion)
            .Map(
                voucher => voucher.VoucherNumber,
                request => request.VoucherNumber.Trim())
            .Map(
                voucher => voucher.ExternalPartyName,
                request => Normalize(request.ExternalPartyName))
            .Map(
                voucher => voucher.ReferenceNumber,
                request => Normalize(request.ReferenceNumber))
            .Map(
                voucher => voucher.Description,
                request => Normalize(request.Description))
            .Map(voucher => voucher.Notes, request => Normalize(request.Notes));

        config.ForType<CashVoucher, CashVoucherResponse>()
            .Map(
                response => response.CashboxName,
                voucher => voucher.Cashbox.Name)
            .Map(
                response => response.CashMovementTypeName,
                voucher => voucher.CashMovementType.Name)
            .Map(
                response => response.BusinessPartnerName,
                voucher => voucher.BusinessPartner == null
                    ? null
                    : voucher.BusinessPartner.Name)
            .Map(
                response => response.DriverName,
                voucher => voucher.Driver == null
                    ? null
                    : voucher.Driver.Name)
            .Map(
                response => response.DriverTripInvoiceNumber,
                voucher => voucher.DriverTrip == null
                    ? null
                    : voucher.DriverTrip.InvoiceNumber)
            .Map(
                response => response.InvoiceNumber,
                voucher => voucher.Invoice == null
                    ? null
                    : voucher.Invoice.InvoiceNumber);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
