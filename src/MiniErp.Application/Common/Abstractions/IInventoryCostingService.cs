using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IInventoryCostingService : IScopedService
{
    Task LockAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default);

    Task<Error?> RecalculateAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, InventoryCostSnapshot>> GetSnapshotsAsync(
        int storeId,
        IReadOnlyCollection<int> itemIds,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryCostingKey(
    int StoreId,
    int ItemId);

public sealed record InventoryCostSnapshot(
    decimal Quantity,
    decimal AverageCost,
    decimal InventoryValue);
