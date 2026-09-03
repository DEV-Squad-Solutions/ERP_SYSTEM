using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public interface IInventoryPostingService
{
    Task<Result> SynchronizeStockAdjustmentAsync(
        int stockAdjustmentId,
        CancellationToken cancellationToken = default);

    Task<Result> SynchronizeStockOpeningBalanceAsync(
        int stockOpeningBalanceId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default);
}
