using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;

namespace MiniErp.Application.Features.Invoices;

public interface IInvoicePostingService
{
    Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);
}
