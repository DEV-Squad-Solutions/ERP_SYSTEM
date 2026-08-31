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
            .Ignore(voucher => voucher.Account)
            .Map(
                voucher => voucher.Description,
                request => Normalize(request.Description));

        config.ForType<CashVoucherUpdateRequest, CashVoucher>()
            .Ignore(voucher => voucher.RowVersion)
            .Ignore(voucher => voucher.ExchangeRateRecord)
            .Ignore(voucher => voucher.ExchangeRateId)
            .Ignore(voucher => voucher.ExchangeRate)
            .Ignore(voucher => voucher.BaseAmount)
            .Ignore(voucher => voucher.InvoicePayment)
            .Ignore(voucher => voucher.Account)
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
                response => response.ExchangeRate,
                voucher => voucher.IsPosted &&
                    voucher.Currency ==
                        (voucher.Company.Settings == null
                            ? Domain.Enums.CurrencyCode.EGP
                            : voucher.Company.Settings.BaseCurrency)
                        ? 1m
                        : voucher.ExchangeRate)
            .Map(
                response => response.BaseAmount,
                voucher => voucher.IsPosted &&
                    voucher.Currency ==
                        (voucher.Company.Settings == null
                            ? Domain.Enums.CurrencyCode.EGP
                            : voucher.Company.Settings.BaseCurrency)
                        ? voucher.Amount
                        : voucher.BaseAmount)
            .Map(
                response => response.CashboxName,
                voucher => voucher.Cashbox == null
                    ? null
                    : voucher.Cashbox.Name)
            .Map(
                response => response.CashMovementTypeName,
                voucher => voucher.CashMovementType == null
                    ? null
                    : voucher.CashMovementType.Name)
            .Map(
                response => response.AccountCode,
                voucher => voucher.Account == null
                    ? null
                    : voucher.Account.Code)
            .Map(
                response => response.AccountName,
                voucher => voucher.Account == null
                    ? null
                    : voucher.Account.Name)
            .Map(
                response => response.AccountType,
                voucher => voucher.Account == null
                    ? null
                    : (Domain.Enums.AccountType?)voucher.Account.AccountType)
            .Map(
                response => response.Classification,
                voucher => voucher.CashMovementType == null
                    ? null
                    : (Domain.Enums.CashMovementClassification?)
                        voucher.CashMovementType.Classification)
            .Map(
                response => response.IsDraft,
                voucher => !voucher.IsPosted)
            .Map(
                response => response.BusinessPartnerName,
                voucher => voucher.BusinessPartner == null
                    ? null
                    : voucher.BusinessPartner.Name)
            .Map(
                response => response.EmployeeName,
                voucher => voucher.Employee == null
                    ? null
                    : voucher.Employee.Name)
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
