using Mapster;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashVoucherRequest, CashVoucher>()
            .Ignore(voucher => voucher.ExchangeRateRecord)
            .Ignore(voucher => voucher.ExchangeRateId)
            .Ignore(voucher => voucher.ExchangeRate)
            .Ignore(voucher => voucher.BaseAmount)
            .Ignore(voucher => voucher.InvoicePayment)
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
            .Ignore(voucher => voucher.ExchangeRateRecord)
            .Ignore(voucher => voucher.ExchangeRateId)
            .Ignore(voucher => voucher.ExchangeRate)
            .Ignore(voucher => voucher.BaseAmount)
            .Ignore(voucher => voucher.InvoicePayment)
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
                response => response.BaseCurrency,
                voucher => voucher.Company.Settings == null
                    ? Domain.Enums.CurrencyCode.EGP
                    : voucher.Company.Settings.BaseCurrency)
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
                    : voucher.Invoice.InvoiceNumber)
            .Map(
                response => response.AppliedInvoiceAmount,
                voucher => voucher.InvoicePayment == null
                    ? null
                    : (decimal?)voucher.InvoicePayment.AppliedAmount)
            .Map(
                response => response.AppliedInvoiceCurrency,
                voucher => voucher.InvoicePayment == null
                    ? null
                    : (Domain.Enums.CurrencyCode?)
                        voucher.InvoicePayment.InvoiceCurrency)
            .Map(
                response => response.AppliedBaseAmount,
                voucher => voucher.InvoicePayment == null
                    ? null
                    : (decimal?)voucher.InvoicePayment.AppliedBaseAmount)
            .Map(
                response => response.RealizedExchangeDifference,
                voucher => voucher.InvoicePayment == null
                    ? null
                    : (decimal?)
                        voucher.InvoicePayment.RealizedExchangeDifference);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
