using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private static IQueryable<Invoice> ApplyFilters(
        IQueryable<Invoice> query,
        InvoiceFilterRequest filters)
    {
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        if (!string.IsNullOrEmpty(invoiceNumber))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(invoiceNumber));
        }

        if (filters.InvoiceType.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceType == filters.InvoiceType.Value);
        }

        if (filters.BusinessPartnerId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.BusinessPartnerId ==
                filters.BusinessPartnerId.Value);
        }

        if (filters.CountryId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.CountryId == filters.CountryId.Value);
        }

        if (filters.StoreId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.StoreId == filters.StoreId.Value);
        }

        if (filters.DriverId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.DriverId == filters.DriverId.Value);
        }

        if (filters.PaymentTerm.HasValue)
        {
            query = query.Where(invoice =>
                invoice.PaymentTerm == filters.PaymentTerm.Value);
        }

        if (filters.PriceStatus == InvoicePriceStatus.HasMissingPrice)
        {
            query = query.Where(invoice =>
                invoice.Lines.Any(line => line.Price == 0m));
        }
        else if (filters.PriceStatus == InvoicePriceStatus.AllItemsPriced)
        {
            query = query.Where(invoice =>
                invoice.Lines.Any() &&
                invoice.Lines.All(line => line.Price > 0m));
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceDate <= filters.ToDate.Value);
        }

        return query;
    }

    private IQueryable<InvoiceResponse> ProjectResponseQuery(int id) =>
        dbContext.Invoices
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.Id == id)
            .ProjectToType<InvoiceResponse>();

    private async Task<Invoice?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .AsSplitQuery()
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .FirstOrDefaultAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.Id == id,
                cancellationToken);
}
