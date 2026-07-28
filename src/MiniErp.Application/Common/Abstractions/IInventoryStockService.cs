using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Common.Abstractions;

public interface IInventoryStockService : IScopedService
{
    Task<IReadOnlyDictionary<int, decimal>> GetBalancesAsync(
        int storeId,
        IReadOnlyCollection<int> itemIds,
        DateOnly asOfDate,
        InventoryMovementReference? excludedMovement = null,
        CancellationToken cancellationToken = default);

    Task<Error?> ValidateTimelineAsync(
        InventoryStockProposal proposal,
        CancellationToken cancellationToken = default);

    Task<bool> HasStockChangesSinceAsync(
        int storeId,
        IReadOnlyCollection<int> itemIds,
        DateTime snapshotTakenAt,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryMovementReference(
    IReadOnlyCollection<ItemMovementType> MovementTypes,
    int ReferenceId,
    string ReferenceNumber);

public sealed record InventoryStockLine(
    int ItemId,
    decimal Quantity);

public sealed record InventoryStockProposal(
    int StoreId,
    DateOnly MovementDate,
    bool IsInbound,
    IReadOnlyCollection<InventoryStockLine> Lines,
    InventoryMovementReference? ReplacedMovement,
    string OperationDescription,
    string ErrorFieldName);
