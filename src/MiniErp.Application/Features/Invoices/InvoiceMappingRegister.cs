using Mapster;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public sealed class InvoiceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        RegisterRequestMappings(config);
        RegisterLineMappings(config);
        RegisterListMapping(config);
        RegisterDetailsMapping(config);
    }

    private static void RegisterRequestMappings(TypeAdapterConfig config)
    {
        config.ForType<InvoiceLineRequest, InvoiceLine>()
            .Map(
                line => line.Notes,
                request => Normalize(request.Notes))
            .Map(
                line => line.ItemName,
                request => Normalize(request.ItemName));

        config.ForType<InvoiceRequest, Invoice>()
            .Ignore(invoice => invoice.Lines)
            .Ignore(invoice => invoice.ContainerLines)
            .Ignore(invoice => invoice.Payments)
            .Ignore(invoice => invoice.ExchangeRateRecord)
            .Ignore(invoice => invoice.ExchangeRateId)
            .Ignore(invoice => invoice.ExchangeRate)
            .Ignore(invoice => invoice.BaseSubtotal)
            .Ignore(invoice => invoice.BaseDiscountAmount)
            .Ignore(invoice => invoice.BaseTotal)
            .Ignore(invoice => invoice.BasePaidAmountAtInvoiceRate)
            .Ignore(invoice => invoice.WBTotal)
            .Map(
                invoice => invoice.InvoiceNumber,
                request => request.InvoiceNumber.Trim())
            .Map(
                invoice => invoice.ExportInvoiceCode,
                request => Normalize(request.ExportInvoiceCode))
            .Map(
                invoice => invoice.PartnerInvoiceNo,
                request => Normalize(request.PartnerInvoiceNo))
            .Map(
                invoice => invoice.ItemsCategoryId,
                request => request.ItemsCategoryId)
            .Map(
                invoice => invoice.WBWeight,
                request => request.WBWeight)
            .Map(
                invoice => invoice.WBScaleDifference,
                request => request.WBScaleDifference)
            .Map(
                invoice => invoice.WBDiscount,
                request => request.WBDiscount)
            .Map(
                invoice => invoice.ExternalDriverName,
                request => Normalize(request.ExternalDriverName))
            .Map(
                invoice => invoice.VehicleNumber,
                request => Normalize(request.VehicleNumber))
            .Map(
                invoice => invoice.Notes,
                request => Normalize(request.Notes));

        config.ForType<InvoiceUpdateRequest, Invoice>()
            .Ignore(invoice => invoice.RowVersion)
            .Ignore(invoice => invoice.Lines)
            .Ignore(invoice => invoice.ContainerLines)
            .Ignore(invoice => invoice.Payments)
            .Ignore(invoice => invoice.ExchangeRateRecord)
            .Ignore(invoice => invoice.ExchangeRateId)
            .Ignore(invoice => invoice.ExchangeRate)
            .Ignore(invoice => invoice.BaseSubtotal)
            .Ignore(invoice => invoice.BaseDiscountAmount)
            .Ignore(invoice => invoice.BaseTotal)
            .Ignore(invoice => invoice.BasePaidAmountAtInvoiceRate)
            .Ignore(invoice => invoice.WBTotal)
            .Map(
                invoice => invoice.ExportInvoiceCode,
                request => Normalize(request.ExportInvoiceCode))
            .Map(
                invoice => invoice.PartnerInvoiceNo,
                request => Normalize(request.PartnerInvoiceNo))
            .Map(
                invoice => invoice.ItemsCategoryId,
                request => request.ItemsCategoryId)
            .Map(
                invoice => invoice.WBWeight,
                request => request.WBWeight)
            .Map(
                invoice => invoice.WBScaleDifference,
                request => request.WBScaleDifference)
            .Map(
                invoice => invoice.WBDiscount,
                request => request.WBDiscount)
            .Map(
                invoice => invoice.ExternalDriverName,
                request => Normalize(request.ExternalDriverName))
            .Map(
                invoice => invoice.VehicleNumber,
                request => Normalize(request.VehicleNumber))
            .Map(
                invoice => invoice.Notes,
                request => Normalize(request.Notes));
    }

    private static void RegisterLineMappings(TypeAdapterConfig config)
    {
        config.ForType<InvoiceLine, InvoiceLineResponse>()
            .Map(response => response.ItemCode, line => line.Item != null ? line.Item.Code : null)
            .Map(response => response.ItemName, line => line.Item != null ? line.Item.Name : line.ItemName)
            .Map(response => response.ItemUnitName, line => line.ItemUnit != null ? line.ItemUnit.Name : null);

        config.ForType<InvoiceContainerLine, InvoiceContainerLineResponse>()
            .Map(response => response.ContainerCode, line => line.Container.Code)
            .Map(response => response.ContainerName, line => line.Container.Name);
    }

    private static void RegisterListMapping(TypeAdapterConfig config)
    {
        config.ForType<Invoice, InvoiceListResponse>()
            .Map(
                response => response.BusinessPartnerName,
                invoice => invoice.BusinessPartner.Name)
            .Map(
                response => response.StoreName,
                invoice => invoice.Store.Name)
            .Map(
                response => response.ContainerStoreName,
                invoice => invoice.ContainerStore == null
                    ? null
                    : invoice.ContainerStore.Name)
            .Map(
                response => response.CountryName,
                invoice => invoice.Country == null ? null : invoice.Country.Name)
            .Map(
                response => response.ItemsCategoryId,
                invoice => invoice.ItemsCategoryId)
            .Map(
                response => response.ItemsCategoryName,
                invoice => invoice.ItemsCategory == null
                    ? null
                    : invoice.ItemsCategory.Name)
            .Map(
                response => response.DriverName,
                invoice => invoice.Driver == null ? null : invoice.Driver.Name)
            .Map(
                response => response.ActualDriverName,
                invoice => invoice.ActualDriver == null
                    ? null
                    : invoice.ActualDriver.Name)
            .Map(
                response => response.BaseCurrency,
                invoice => invoice.Company == null ||
                    invoice.Company.Settings == null
                    ? CurrencyCode.EGP
                    : invoice.Company.Settings.BaseCurrency)
            .Map(
                response => response.PaymentStatus,
                invoice => invoice.PaidAmount <= 0m && invoice.Total > 0m
                    ? PaymentStatus.Unpaid
                    : invoice.Total - invoice.PaidAmount <= 0m
                    ? PaymentStatus.Paid
                    : PaymentStatus.PartiallyPaid)
            .Map(
                response => response.PaymentVoucherId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.Id)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.CashboxId)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxName,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => voucher.Cashbox == null
                        ? null
                        : voucher.Cashbox.Name)
                    .FirstOrDefault())
            .Map(
                response => response.CashMovementTypeId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.CashMovementTypeId)
                    .FirstOrDefault())
            .Map(
                response => response.CashMovementTypeName,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => voucher.CashMovementType == null
                        ? null
                        : voucher.CashMovementType.Name)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxCurrency,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (CurrencyCode?)payment.CashboxCurrency)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxAmount,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment => (decimal?)payment.CashboxAmount)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxExchangeRate,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.CashboxToBaseRate)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxBaseAmount,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.CashboxBaseAmount)
                    .FirstOrDefault())
            .Map(
                response => response.RealizedExchangeDifference,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.RealizedExchangeDifference)
                    .FirstOrDefault())
            .Map(
                response => response.Subtotal,
                invoice => invoice.Lines.Sum(line => line.Total))
            .Map(
                response => response.RemainingAmount,
                invoice => invoice.Total - invoice.PaidAmount)
            .Map(
                response => response.LineCount,
                invoice => invoice.Lines.Count)
            .Map(
                response => response.ContainerLineCount,
                invoice => invoice.ContainerLines.Count);
    }

    private static void RegisterDetailsMapping(TypeAdapterConfig config)
    {
        config.ForType<Invoice, InvoiceResponse>()
            .Map(
                response => response.BusinessPartnerName,
                invoice => invoice.BusinessPartner.Name)
            .Map(
                response => response.StoreName,
                invoice => invoice.Store.Name)
            .Map(
                response => response.ContainerStoreName,
                invoice => invoice.ContainerStore == null
                    ? null
                    : invoice.ContainerStore.Name)
            .Map(
                response => response.CountryName,
                invoice => invoice.Country == null ? null : invoice.Country.Name)
            .Map(
                response => response.ItemsCategoryId,
                invoice => invoice.ItemsCategoryId)
            .Map(
                response => response.ItemsCategoryName,
                invoice => invoice.ItemsCategory == null
                    ? null
                    : invoice.ItemsCategory.Name)
            .Map(
                response => response.DriverName,
                invoice => invoice.Driver == null ? null : invoice.Driver.Name)
            .Map(
                response => response.ActualDriverName,
                invoice => invoice.ActualDriver == null
                    ? null
                    : invoice.ActualDriver.Name)
            .Map(
                response => response.BaseCurrency,
                invoice => invoice.Company == null ||
                    invoice.Company.Settings == null
                    ? CurrencyCode.EGP
                    : invoice.Company.Settings.BaseCurrency)
            .Map(
                response => response.PaymentStatus,
                invoice => invoice.PaidAmount <= 0m && invoice.Total > 0m
                    ? PaymentStatus.Unpaid
                    : invoice.Total - invoice.PaidAmount <= 0m
                    ? PaymentStatus.Paid
                    : PaymentStatus.PartiallyPaid)
            .Map(
                response => response.PaymentVoucherId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.Id)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.CashboxId)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxName,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => voucher.Cashbox == null
                        ? null
                        : voucher.Cashbox.Name)
                    .FirstOrDefault())
            .Map(
                response => response.CashMovementTypeId,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => (int?)voucher.CashMovementTypeId)
                    .FirstOrDefault())
            .Map(
                response => response.CashMovementTypeName,
                invoice => invoice.PaymentVouchers
                    .OrderBy(voucher => voucher.Id)
                    .Select(voucher => voucher.CashMovementType == null
                        ? null
                        : voucher.CashMovementType.Name)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxCurrency,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (CurrencyCode?)payment.CashboxCurrency)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxAmount,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment => (decimal?)payment.CashboxAmount)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxExchangeRate,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.CashboxToBaseRate)
                    .FirstOrDefault())
            .Map(
                response => response.CashboxBaseAmount,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.CashboxBaseAmount)
                    .FirstOrDefault())
            .Map(
                response => response.RealizedExchangeDifference,
                invoice => invoice.Payments
                    .OrderBy(payment => payment.Id)
                    .Select(payment =>
                        (decimal?)payment.RealizedExchangeDifference)
                    .FirstOrDefault())
            .Map(
                response => response.Subtotal,
                invoice => invoice.Lines.Sum(line => line.Total))
            .Map(
                response => response.RemainingAmount,
                invoice => invoice.Total - invoice.PaidAmount)
            .Map(
                response => response.Lines,
                invoice => invoice.Lines.OrderBy(line => line.Id))
            .Map(
                response => response.ContainerLines,
                invoice => invoice.ContainerLines.OrderBy(line => line.Id));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
