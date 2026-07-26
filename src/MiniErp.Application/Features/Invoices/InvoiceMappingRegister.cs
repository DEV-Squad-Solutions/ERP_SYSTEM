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
                request => Normalize(request.Notes));

        config.ForType<InvoiceRequest, Invoice>()
            .Ignore(invoice => invoice.Lines)
            .Ignore(invoice => invoice.ContainerLines)
            .Map(
                invoice => invoice.ExportInvoiceCode,
                request => Normalize(request.ExportInvoiceCode))
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
            .Map(
                invoice => invoice.ExportInvoiceCode,
                request => Normalize(request.ExportInvoiceCode))
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
            .Map(response => response.ItemCode, line => line.Item.Code)
            .Map(response => response.ItemName, line => line.Item.Name)
            .Map(response => response.ItemUnitName, line => line.ItemUnit.Name);

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
                response => response.DriverName,
                invoice => invoice.Driver == null ? null : invoice.Driver.Name)
            .Map(
                response => response.ActualDriverName,
                invoice => invoice.ActualDriver == null
                    ? null
                    : invoice.ActualDriver.Name)
            .Map(
                response => response.PaymentStatus,
                invoice => invoice.Total - invoice.PaidAmount <= 0m
                    ? PaymentStatus.Paid
                    : PaymentStatus.Unpaid)
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
                invoice => invoice.ContainerLines.Count)
            .Map(
                response => response.Lines,
                invoice => invoice.Lines.OrderBy(line => line.Id))
            .Map(
                response => response.ContainerLines,
                invoice => invoice.ContainerLines.OrderBy(line => line.Id));
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
                response => response.DriverName,
                invoice => invoice.Driver == null ? null : invoice.Driver.Name)
            .Map(
                response => response.ActualDriverName,
                invoice => invoice.ActualDriver == null
                    ? null
                    : invoice.ActualDriver.Name)
            .Map(
                response => response.PaymentStatus,
                invoice => invoice.Total - invoice.PaidAmount <= 0m
                    ? PaymentStatus.Paid
                    : PaymentStatus.Unpaid)
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
