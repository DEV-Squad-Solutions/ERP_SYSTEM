using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
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
