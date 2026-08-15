using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
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

    private Task<bool> HasActiveLinkedReturnsAsync(
        IReadOnlyCollection<int> sourceLineIds,
        CancellationToken cancellationToken) =>
        sourceLineIds.Count > 0
            ? dbContext.InvoiceLines.AnyAsync(
                line =>
                    line.CompanyId == companyId &&
                    line.SourceInvoiceLineId.HasValue &&
                    sourceLineIds.Contains(
                        line.SourceInvoiceLineId.Value) &&
                    (line.Invoice.InvoiceType == InvoiceType.SalesReturn ||
                     line.Invoice.InvoiceType == InvoiceType.PurchaseReturn),
                cancellationToken)
            : Task.FromResult(false);
}
