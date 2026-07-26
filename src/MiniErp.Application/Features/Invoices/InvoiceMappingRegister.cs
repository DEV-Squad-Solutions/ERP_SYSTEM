using Mapster;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public sealed class InvoiceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<InvoiceLineRequest, InvoiceLine>()
            .Ignore(line => line.Id)
            .Ignore(line => line.CompanyId)
            .Ignore(line => line.InvoiceId)
            .Ignore(line => line.Invoice)
            .Ignore(line => line.Item)
            .Ignore(line => line.ItemUnit)
            .Ignore(line => line.Quantity)
            .Ignore(line => line.Total)
            .Ignore(line => line.CreatedById)
            .Ignore(line => line.CreatedOn)
            .Ignore(line => line.CreatedByPc)
            .Ignore(line => line.UpdatedById)
            .Ignore(line => line.UpdatedOn)
            .Ignore(line => line.UpdatedByPc)
            .Ignore(line => line.DeletedById)
            .Ignore(line => line.DeletedOn)
            .Ignore(line => line.DeletedByPc)
            .Ignore(line => line.IsDeleted)
            .Map(line => line.Notes, request => Normalize(request.Notes));

        config.ForType<InvoiceContainerLineRequest, InvoiceContainerLine>()
            .Ignore(line => line.Id)
            .Ignore(line => line.CompanyId)
            .Ignore(line => line.InvoiceId)
            .Ignore(line => line.Invoice)
            .Ignore(line => line.Container)
            .Ignore(line => line.CreatedById)
            .Ignore(line => line.CreatedOn)
            .Ignore(line => line.CreatedByPc)
            .Ignore(line => line.UpdatedById)
            .Ignore(line => line.UpdatedOn)
            .Ignore(line => line.UpdatedByPc)
            .Ignore(line => line.DeletedById)
            .Ignore(line => line.DeletedOn)
            .Ignore(line => line.DeletedByPc)
            .Ignore(line => line.IsDeleted);

        config.ForType<InvoiceRequest, Invoice>()
            .Ignore(invoice => invoice.Id)
            .Ignore(invoice => invoice.CompanyId)
            .Ignore(invoice => invoice.InvoiceNumber)
            .Ignore(invoice => invoice.Currency)
            .Ignore(invoice => invoice.Total)
            .Ignore(invoice => invoice.RowVersion)
            .Ignore(invoice => invoice.LastModifiedAt)
            .Ignore(invoice => invoice.Lines)
            .Ignore(invoice => invoice.ContainerLines)
            .Ignore(invoice => invoice.Company)
            .Ignore(invoice => invoice.BusinessPartner)
            .Ignore(invoice => invoice.Store)
            .Ignore(invoice => invoice.ContainerStore)
            .Ignore(invoice => invoice.Country)
            .Ignore(invoice => invoice.Driver)
            .Ignore(invoice => invoice.ActualDriver)
            .Ignore(invoice => invoice.CreatedById)
            .Ignore(invoice => invoice.CreatedOn)
            .Ignore(invoice => invoice.CreatedByPc)
            .Ignore(invoice => invoice.UpdatedById)
            .Ignore(invoice => invoice.UpdatedOn)
            .Ignore(invoice => invoice.UpdatedByPc)
            .Ignore(invoice => invoice.DeletedById)
            .Ignore(invoice => invoice.DeletedOn)
            .Ignore(invoice => invoice.DeletedByPc)
            .Ignore(invoice => invoice.IsDeleted)
            .Map(invoice => invoice.ExportInvoiceCode, request =>
                Normalize(request.ExportInvoiceCode))
            .Map(invoice => invoice.ExternalDriverName, request =>
                Normalize(request.ExternalDriverName))
            .Map(invoice => invoice.VehicleNumber, request =>
                Normalize(request.VehicleNumber))
            .Map(invoice => invoice.Notes, request => Normalize(request.Notes));

        config.ForType<InvoiceUpdateRequest, Invoice>()
            .Ignore(invoice => invoice.Id)
            .Ignore(invoice => invoice.CompanyId)
            .Ignore(invoice => invoice.InvoiceNumber)
            .Ignore(invoice => invoice.Currency)
            .Ignore(invoice => invoice.Total)
            .Ignore(invoice => invoice.RowVersion)
            .Ignore(invoice => invoice.LastModifiedAt)
            .Ignore(invoice => invoice.Lines)
            .Ignore(invoice => invoice.ContainerLines)
            .Ignore(invoice => invoice.Company)
            .Ignore(invoice => invoice.BusinessPartner)
            .Ignore(invoice => invoice.Store)
            .Ignore(invoice => invoice.ContainerStore)
            .Ignore(invoice => invoice.Country)
            .Ignore(invoice => invoice.Driver)
            .Ignore(invoice => invoice.ActualDriver)
            .Ignore(invoice => invoice.CreatedById)
            .Ignore(invoice => invoice.CreatedOn)
            .Ignore(invoice => invoice.CreatedByPc)
            .Ignore(invoice => invoice.UpdatedById)
            .Ignore(invoice => invoice.UpdatedOn)
            .Ignore(invoice => invoice.UpdatedByPc)
            .Ignore(invoice => invoice.DeletedById)
            .Ignore(invoice => invoice.DeletedOn)
            .Ignore(invoice => invoice.DeletedByPc)
            .Ignore(invoice => invoice.IsDeleted)
            .Map(invoice => invoice.ExportInvoiceCode, request =>
                Normalize(request.ExportInvoiceCode))
            .Map(invoice => invoice.ExternalDriverName, request =>
                Normalize(request.ExternalDriverName))
            .Map(invoice => invoice.VehicleNumber, request =>
                Normalize(request.VehicleNumber))
            .Map(invoice => invoice.Notes, request => Normalize(request.Notes));

        RegisterLineMapping(config);
        RegisterContainerLineMapping(config);
        RegisterHeaderMapping<InvoiceListResponse>(config);
        RegisterHeaderMapping<InvoiceResponse>(config);
    }

    private static void RegisterLineMapping(TypeAdapterConfig config)
    {
        config.ForType<InvoiceLine, InvoiceLineResponse>()
            .Map(response => response.ItemCode, line => line.Item.Code)
            .Map(response => response.ItemName, line => line.Item.Name)
            .Map(response => response.ItemUnitName, line => line.ItemUnit.Name);
    }

    private static void RegisterContainerLineMapping(TypeAdapterConfig config)
    {
        config.ForType<InvoiceContainerLine, InvoiceContainerLineResponse>()
            .Map(response => response.ContainerCode, line => line.Container.Code)
            .Map(response => response.ContainerName, line => line.Container.Name);
    }

    private static void RegisterHeaderMapping<TResponse>(
        TypeAdapterConfig config)
    {
        config.ForType<Invoice, TResponse>()
            .Map("BusinessPartnerName", invoice => invoice.BusinessPartner.Name)
            .Map("StoreName", invoice => invoice.Store.Name)
            .Map(
                "ContainerStoreName",
                invoice => invoice.ContainerStore == null
                    ? null
                    : invoice.ContainerStore.Name)
            .Map(
                "CountryName",
                invoice => invoice.Country == null ? null : invoice.Country.Name)
            .Map(
                "DriverName",
                invoice => invoice.Driver == null ? null : invoice.Driver.Name)
            .Map(
                "ActualDriverName",
                invoice => invoice.ActualDriver == null
                    ? null
                    : invoice.ActualDriver.Name)
            .Map(
                "PaymentStatus",
                invoice => invoice.Total - invoice.PaidAmount <= 0m
                    ? PaymentStatus.Paid
                    : PaymentStatus.Unpaid)
            .Map(
                "Subtotal",
                invoice => invoice.Lines.Sum(line => line.Total))
            .Map(
                "DiscountAmount",
                invoice => invoice.DiscountAmount)
            .Map(
                "PaidAmount",
                invoice => invoice.PaidAmount)
            .Map(
                "RemainingAmount",
                invoice => invoice.Total - invoice.PaidAmount)
            .Map(
                "Lines",
                invoice => invoice.Lines.OrderBy(line => line.Id))
            .Map(
                "ContainerLines",
                invoice => invoice.ContainerLines.OrderBy(line => line.Id));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
