using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IInventoryCostPostingSynchronizer : IScopedService
{
    Task<Error?> SynchronizeAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default);
}
