using System.Data;
using static MiniErp.Application.Features.StockAdjustments.StockAdjustmentErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockAdjustments;

public sealed partial class StockAdjustmentService
{
    private IQueryable<StockAdjustmentResponse> ProjectResponseQuery(
        int id,
        int? fiscalYearId = null) =>
        dbContext.StockAdjustments
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.Id == id &&
                (!fiscalYearId.HasValue ||
                 adjustment.FiscalYearId == fiscalYearId.Value))
            .ProjectToType<StockAdjustmentResponse>();

    private async Task<StockAdjustmentResponse> EnrichResponseAsync(
        StockAdjustmentResponse response,
        CancellationToken cancellationToken)
    {
        var snapshots = await LoadCostSnapshotsAsync(
            [response.Id],
            cancellationToken);
        return response with
        {
            Lines = response.Lines
                .Select(line => EnrichLine(
                    response.Id,
                    line,
                    snapshots))
                .ToArray()
        };
    }

    private async Task<IReadOnlyList<StockAdjustmentListResponse>>
        EnrichListResponsesAsync(
            IReadOnlyList<StockAdjustmentListResponse> responses,
            CancellationToken cancellationToken)
    {
        var snapshots = await LoadCostSnapshotsAsync(
            responses.Select(response => response.Id).ToArray(),
            cancellationToken);
        return responses
            .Select(response => response with
            {
                Lines = response.Lines
                    .Select(line => EnrichLine(
                        response.Id,
                        line,
                        snapshots))
                    .ToArray()
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<
        (int ReferenceId, int ItemId),
        MovementCostSnapshot>> LoadCostSnapshotsAsync(
            IReadOnlyCollection<int> adjustmentIds,
            CancellationToken cancellationToken)
    {
        if (adjustmentIds.Count == 0)
        {
            return new Dictionary<
                (int ReferenceId, int ItemId),
                MovementCostSnapshot>();
        }

        var movementTypes = AdjustmentMovementTypes;
        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movementTypes.Contains(movement.MovementType) &&
                adjustmentIds.Contains(movement.ReferenceId))
            .Select(movement => new MovementCostSnapshot(
                movement.ReferenceId,
                movement.ItemId,
                movement.CostStatus,
                movement.PendingCostQuantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.QuantityAfter,
                movement.AverageCostAfter,
                movement.InventoryValueAfter))
            .ToListAsync(cancellationToken);

        return movements.ToDictionary(
            movement => (movement.ReferenceId, movement.ItemId));
    }

    private static StockAdjustmentLineResponse EnrichLine(
        int adjustmentId,
        StockAdjustmentLineResponse line,
        IReadOnlyDictionary<
            (int ReferenceId, int ItemId),
            MovementCostSnapshot> snapshots)
    {
        if (!snapshots.TryGetValue(
                (adjustmentId, line.ItemId),
                out var snapshot))
        {
            return line;
        }

        return line with
        {
            CostStatus = snapshot.CostStatus,
            PendingCostQuantity = snapshot.PendingCostQuantity,
            UnitCost = line.UnitCost ?? snapshot.UnitCost,
            InventoryTotalCost = snapshot.TotalCost,
            QuantityAfter = snapshot.QuantityAfter,
            AverageCostAfter = snapshot.AverageCostAfter,
            InventoryValueAfter = snapshot.InventoryValueAfter
        };
    }

    private sealed record MovementCostSnapshot(
        int ReferenceId,
        int ItemId,
        InventoryCostStatus CostStatus,
        decimal PendingCostQuantity,
        decimal? UnitCost,
        decimal TotalCost,
        decimal QuantityAfter,
        decimal AverageCostAfter,
        decimal InventoryValueAfter);

    private Task<StockAdjustment?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.StockAdjustments
            .Include(adjustment => adjustment.Lines)
            .FirstOrDefaultAsync(
                adjustment =>
                    adjustment.CompanyId == companyId &&
                    adjustment.Id == id,
                cancellationToken);

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateRequestAsync(
            int storeId,
            StockAdjustmentDirection direction,
            IReadOnlyList<StockAdjustmentLineRequest> lines,
            CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(direction))
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(DirectionInvalid());
        }

        if (lines.Count is < 1 or > StockAdjustmentRequest.MaximumLineCount ||
            lines.Any(line => line.ItemId <= 0 || line.Quantity <= 0m) ||
            lines.Select(line => line.ItemId).Distinct().Count() != lines.Count)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(LinesInvalid());
        }

        if (direction == StockAdjustmentDirection.Increase &&
            lines.Any(line => !line.UnitCost.HasValue))
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(UnitCostRequired());
        }

        if (direction == StockAdjustmentDirection.Decrease &&
            lines.Any(line => line.UnitCost.HasValue))
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(UnitCostNotAllowed());
        }

        if (lines.Any(line => line.UnitCost < 0m))
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(UnitCostInvalid());
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == storeId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                StoreNotFound(storeId));
        }

        if (!store.IsActive)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                StoreInactive());
        }

        if (store.IsContainerStore)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ContainerStoreNotAllowed());
        }

        var itemIds = lines
            .Select(line => line.ItemId)
            .Distinct()
            .ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIds.Contains(item.Id))
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                item.ItemUnit.IsActive))
            .ToListAsync(cancellationToken);
        var itemsById = items.ToDictionary(item => item.Id);

        var missingItemIds = itemIds
            .Where(itemId => !itemsById.ContainsKey(itemId))
            .ToArray();
        if (missingItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemNotFound(missingItemIds));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemInactive(inactiveItemIds));
        }

        var inactiveUnitItemIds = items
            .Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id)
            .ToArray();
        return inactiveUnitItemIds.Length == 0
            ? Result<IReadOnlyDictionary<int, ItemSnapshot>>.Success(itemsById)
            : Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemUnitInactive(inactiveUnitItemIds));
    }

    private Task<bool> DocumentNumberExistsAsync(
        string documentNumber,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.StockAdjustments.AnyAsync(
            adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.DocumentNumber == documentNumber &&
                (!excludedId.HasValue ||
                 adjustment.Id != excludedId.Value),
            cancellationToken);

    private void AddLines(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        foreach (var request in requests)
        {
            var item = items[request.ItemId];
            var line = request.Adapt<StockAdjustmentLine>();
            line.CompanyId = companyId;
            line.ItemUnitId = item.ItemUnitId;
            line.UnitCost =
                adjustment.Direction == StockAdjustmentDirection.Increase
                    ? request.UnitCost
                    : null;
            adjustment.Lines.Add(line);
        }
    }

    private void ReplaceLines(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        var incomingByItem = requests.ToDictionary(line => line.ItemId);
        foreach (var existingLine in adjustment.Lines.ToList())
        {
            if (!incomingByItem.TryGetValue(
                    existingLine.ItemId,
                    out var incoming))
            {
                dbContext.StockAdjustmentLines.Remove(existingLine);
                adjustment.Lines.Remove(existingLine);
                continue;
            }

            existingLine.ItemUnitId = items[incoming.ItemId].ItemUnitId;
            existingLine.Quantity = incoming.Quantity;
            existingLine.UnitCost =
                adjustment.Direction == StockAdjustmentDirection.Increase
                    ? incoming.UnitCost
                    : null;
            existingLine.Reason = string.IsNullOrWhiteSpace(incoming.Reason)
                ? null
                : incoming.Reason.Trim();
        }

        var existingItemIds = adjustment.Lines
            .Select(line => line.ItemId)
            .ToHashSet();
        foreach (var incoming in requests.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            var line = incoming.Adapt<StockAdjustmentLine>();
            line.CompanyId = companyId;
            line.ItemUnitId = items[incoming.ItemId].ItemUnitId;
            line.UnitCost =
                adjustment.Direction == StockAdjustmentDirection.Increase
                    ? incoming.UnitCost
                    : null;
            adjustment.Lines.Add(line);
        }
    }

    private async Task<Error?> ValidateStockAsync(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> lines,
        InventoryMovementReference? replacedMovement,
        CancellationToken cancellationToken) =>
        await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                adjustment.StoreId,
                adjustment.DocumentDate,
                StockAdjustmentMovementRules.IsInbound(
                    adjustment.Direction),
                lines
                    .Select(line => new InventoryStockLine(
                        line.ItemId,
                        line.Quantity))
                    .ToArray(),
                replacedMovement,
                replacedMovement is null
                    ? $"إضافة تسوية المخزون {adjustment.DocumentNumber}"
                    : $"تعديل تسوية المخزون {adjustment.DocumentNumber}",
                nameof(StockAdjustmentRequest.Lines)),
            cancellationToken);

    private void AddMovements(StockAdjustment adjustment)
    {
        var inbound = StockAdjustmentMovementRules.IsInbound(
            adjustment.Direction);
        var movementType = StockAdjustmentMovementRules.GetMovementType(
            adjustment.Direction);

        foreach (var line in adjustment.Lines.Where(line => !line.IsDeleted))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = adjustment.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = movementType,
                    ReferenceId = adjustment.Id,
                    ReferenceNumber = adjustment.DocumentNumber,
                    MovementDate = adjustment.DocumentDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description =
                        $"Stock adjustment {adjustment.DocumentNumber}"
                });
        }
    }

    private void ReconcileMovements(
        StockAdjustment adjustment,
        IReadOnlyCollection<ItemMovement> existingMovements)
    {
        var activeLines = adjustment.Lines
            .Where(line => !line.IsDeleted)
            .ToDictionary(line => line.ItemId);
        var existingItemIds = new HashSet<int>();
        var inbound = StockAdjustmentMovementRules.IsInbound(
            adjustment.Direction);
        var movementType = StockAdjustmentMovementRules.GetMovementType(
            adjustment.Direction);

        foreach (var movement in existingMovements)
        {
            if (!activeLines.TryGetValue(movement.ItemId, out var line))
            {
                dbContext.ItemMovements.Remove(movement);
                continue;
            }

            existingItemIds.Add(line.ItemId);
            movement.StoreId = adjustment.StoreId;
            movement.ItemUnitId = line.ItemUnitId;
            movement.MovementType = movementType;
            movement.ReferenceNumber = adjustment.DocumentNumber;
            movement.MovementDate = adjustment.DocumentDate;
            movement.QuantityIn = inbound ? line.Quantity : 0m;
            movement.QuantityOut = inbound ? 0m : line.Quantity;
            movement.Description =
                $"Stock adjustment {adjustment.DocumentNumber}";
        }

        foreach (var line in activeLines.Values.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = adjustment.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = movementType,
                    ReferenceId = adjustment.Id,
                    ReferenceNumber = adjustment.DocumentNumber,
                    MovementDate = adjustment.DocumentDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description =
                        $"Stock adjustment {adjustment.DocumentNumber}"
                });
        }
    }

    private Task<List<ItemMovement>> LoadMovementsAsync(
        int adjustmentId,
        string documentNumber,
        CancellationToken cancellationToken) =>
        dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                AdjustmentMovementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == adjustmentId &&
                movement.ReferenceNumber == documentNumber)
            .ToListAsync(cancellationToken);

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        StockAdjustment adjustment) =>
        adjustment.Lines
            .Where(line => !line.IsDeleted)
            .Select(line => new InventoryCostingKey(
                adjustment.StoreId,
                line.ItemId,
                adjustment.FiscalYearId))
            .Distinct()
            .ToArray();

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        IEnumerable<ItemMovement> movements) =>
        movements
            .Select(movement => new InventoryCostingKey(
                movement.StoreId,
                movement.ItemId,
                movement.FiscalYearId))
            .Distinct()
            .ToArray();

    private static InventoryMovementReference MovementReference(
        int adjustmentId,
        string documentNumber) =>
        new(
            AdjustmentMovementTypes,
            adjustmentId,
            documentNumber);

    private static Error? ValidateFilters(
        StockAdjustmentFilterRequest filters)
    {
        if (filters.FiscalYearId is <= 0 ||
            filters.StoreId is <= 0 ||
            filters.Direction.HasValue &&
            !Enum.IsDefined(filters.Direction.Value) ||
            filters.ToDate < filters.FromDate)
        {
            return FiltersInvalid();
        }

        return null;
    }

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId,
        bool IsActive,
        bool ItemUnitIsActive);

    private static bool IsDocumentNumberConflict(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains(
                   "IX_StockAdjustments_CompanyId_DocumentNumber",
                   StringComparison.OrdinalIgnoreCase) ||
               message.Contains(
                   "UX_StockAdjustments_Company_Document",
                   StringComparison.OrdinalIgnoreCase);
    }
}
