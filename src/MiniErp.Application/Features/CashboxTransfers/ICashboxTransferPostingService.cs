using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;

namespace MiniErp.Application.Features.CashboxTransfers;

public interface ICashboxTransferPostingService
{
    Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        int transferId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int transferId,
        CancellationToken cancellationToken = default);
}
