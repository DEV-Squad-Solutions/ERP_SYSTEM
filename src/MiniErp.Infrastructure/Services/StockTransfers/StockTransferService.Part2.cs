using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockTransfers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.StockTransfers.StockTransferErrors;

namespace MiniErp.Infrastructure.Services.StockTransfers;

public sealed partial class StockTransferService
{
    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>> PrepareAsync(
        int sourceStoreId,
        int destinationStoreId,
        IReadOnlyCollection<StockTransferLineRequest> lines,
        CancellationToken cancellationToken)
    {
        if (sourceStoreId == destinationStoreId)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                StoresMustDiffer());
        }

        var stores = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.CompanyId == companyId &&
                (store.Id == sourceStoreId || store.Id == destinationStoreId))
            .Select(store => new
            {
                store.Id,
                store.IsActive,
                store.IsContainerStore
            })
            .ToListAsync(cancellationToken);
        foreach (var (storeId, fieldName) in new[]
                 {
                     (sourceStoreId, nameof(StockTransferRequest.SourceStoreId)),
                     (destinationStoreId,
                         nameof(StockTransferRequest.DestinationStoreId))
                 })
        {
            var store = stores.SingleOrDefault(candidate => candidate.Id == storeId);
            if (store is null)
            {
                return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                    StoreNotFound(storeId, fieldName));
            }

            if (!store.IsActive)
            {
                return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                    StoreInactive(storeId, fieldName));
            }

            if (store.IsContainerStore)
            {
                return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                    ContainerStoreNotAllowed(storeId, fieldName));
            }
        }

        var itemIds = lines.Select(line => line.ItemId).Distinct().ToArray();
        var itemData = await dbContext.Items
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && itemIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                ItemUnitIsActive = item.ItemUnit != null &&
                    item.ItemUnit.IsActive
            })
            .ToListAsync(cancellationToken);
        var items = itemData.Select(item => new ItemSnapshot(
            Id: item.Id,
            ItemUnitId: item.ItemUnitId,
            IsActive: item.IsActive,
            ItemUnitIsActive: item.ItemUnitIsActive))
            .ToList();
        var missingIds = itemIds.Except(items.Select(item => item.Id)).ToArray();
        if (missingIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemNotFound(missingIds));
        }

        var inactiveIds = items.Where(item => !item.IsActive)
            .Select(item => item.Id).ToArray();
        if (inactiveIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemInactive(inactiveIds));
        }

        var inactiveUnitIds = items.Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id).ToArray();
        if (inactiveUnitIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemUnitInactive(inactiveUnitIds));
        }

        return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Success(
            items.ToDictionary(item => item.Id));
    }

    private static Error? ValidateRequestShape(
        DateOnly transferDate,
        int sourceStoreId,
        int destinationStoreId,
        IReadOnlyCollection<StockTransferLineRequest>? lines)
    {
        if (sourceStoreId <= 0 || destinationStoreId <= 0)
        {
            return Error.Validation(
                "StockTransfers.StoreInvalid",
                "اختر مخزن المصدر ومخزن الوجهة.");
        }

        if (sourceStoreId == destinationStoreId)
        {
            return StoresMustDiffer();
        }

        return ValidateLines(transferDate, lines);
    }

    private static Error? ValidateLines(
        DateOnly transferDate,
        IReadOnlyCollection<StockTransferLineRequest>? lines)
    {
        if (transferDate == default)
        {
            return Error.Validation(
                "StockTransfers.TransferDateRequired",
                "تاريخ التحويل مطلوب.",
                nameof(StockTransferRequest.TransferDate));
        }

        if (lines is null || lines.Count is 0 or >
            StockTransferRequest.MaximumLineCount ||
            lines.Any(line => line.ItemId <= 0 ||
                line.Quantity <= 0m) ||
            lines.Select(line => line.ItemId).Distinct().Count() != lines.Count)
        {
            return Error.Validation(
                "StockTransfers.LinesInvalid",
                "أرسل أصنافاً غير مكررة بكميات موجبة وصحيحة.",
                nameof(StockTransferRequest.Lines));
        }

        return null;
    }

    private Task<Error?> ValidateStockAsync(
        int storeId,
        DateOnly date,
        IReadOnlyCollection<StockTransferLineRequest> lines,
        InventoryMovementReference? replacedMovement,
        string operationDescription,
        CancellationToken cancellationToken) =>
        inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: storeId,
                MovementDate: date,
                IsInbound: false,
                Lines: lines.Select(line =>
                    new InventoryStockLine(line.ItemId, line.Quantity)).ToArray(),
                ReplacedMovement: replacedMovement,
                OperationDescription: operationDescription,
                ErrorFieldName: nameof(StockTransferRequest.Lines),
                DateCoverageErrorWhenChecksDisabled:
                    MiniErp.Application.Features.Inventory.InventoryErrors
                        .TransferUnitCostRequired()),
            cancellationToken);

    private void AddLines(
        StockTransfer transfer,
        IReadOnlyCollection<StockTransferLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        foreach (var request in requests)
        {
            transfer.Lines.Add(new StockTransferLine
            {
                CompanyId = companyId,
                ItemId = request.ItemId,
                ItemUnitId = items[request.ItemId].ItemUnitId,
                Quantity = request.Quantity,
                Notes = Normalize(request.Notes)
            });
        }
    }

    private void ReplaceLines(
        StockTransfer transfer,
        IReadOnlyCollection<StockTransferLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        var incoming = requests.ToDictionary(request => request.ItemId);
        foreach (var line in transfer.Lines.ToList())
        {
            if (!incoming.Remove(line.ItemId, out var request))
            {
                dbContext.StockTransferLines.Remove(line);
                continue;
            }

            line.ItemUnitId = items[request.ItemId].ItemUnitId;
            line.Quantity = request.Quantity;
            line.Notes = Normalize(request.Notes);
        }

        AddLines(transfer, incoming.Values, items);
    }

    private void AddMovements(StockTransfer transfer)
    {
        foreach (var line in transfer.Lines.Where(line => !line.IsDeleted))
        {
            dbContext.ItemMovements.AddRange(
                CreateMovement(transfer, line, ItemMovementType.TransferOut),
                CreateMovement(transfer, line, ItemMovementType.TransferIn));
        }
    }

    private ItemMovement CreateMovement(
        StockTransfer transfer,
        StockTransferLine line,
        ItemMovementType movementType) =>
        new()
        {
            CompanyId = companyId,
            StoreId = movementType == ItemMovementType.TransferOut
                ? transfer.SourceStoreId
                : transfer.DestinationStoreId,
            ItemId = line.ItemId,
            ItemUnitId = line.ItemUnitId,
            MovementType = movementType,
            ReferenceId = transfer.Id,
            ReferenceNumber = transfer.DocumentNumber,
            MovementDate = transfer.TransferDate,
            QuantityIn = movementType == ItemMovementType.TransferIn
                ? line.Quantity
                : 0m,
            QuantityOut = movementType == ItemMovementType.TransferOut
                ? line.Quantity
                : 0m,
            Description = $"Stock transfer {transfer.DocumentNumber}"
        };

    private void ReconcileMovements(
        StockTransfer transfer,
        IReadOnlyCollection<ItemMovement> existingMovements)
    {
        var lines = transfer.Lines.Where(line =>
                !line.IsDeleted &&
                dbContext.Entry(line).State != EntityState.Deleted)
            .ToDictionary(line => line.ItemId);
        var existingKeys = new HashSet<(ItemMovementType Type, int ItemId)>();
        foreach (var movement in existingMovements)
        {
            if (!lines.TryGetValue(movement.ItemId, out var line))
            {
                dbContext.ItemMovements.Remove(movement);
                continue;
            }

            existingKeys.Add((movement.MovementType, movement.ItemId));
            movement.StoreId = movement.MovementType == ItemMovementType.TransferOut
                ? transfer.SourceStoreId
                : transfer.DestinationStoreId;
            movement.ItemUnitId = line.ItemUnitId;
            movement.MovementDate = transfer.TransferDate;
            movement.QuantityIn = movement.MovementType == ItemMovementType.TransferIn
                ? line.Quantity
                : 0m;
            movement.QuantityOut = movement.MovementType == ItemMovementType.TransferOut
                ? line.Quantity
                : 0m;
            movement.Description = $"Stock transfer {transfer.DocumentNumber}";
        }

        foreach (var line in lines.Values)
        {
            foreach (var type in TransferMovementTypes)
            {
                if (!existingKeys.Contains((type, line.ItemId)))
                {
                    dbContext.ItemMovements.Add(CreateMovement(transfer, line, type));
                }
            }
        }
    }

    private async Task<StockTransferResponse?> BuildResponseAsync(
        int id,
        CancellationToken cancellationToken,
        int? fiscalYearId = null)
    {
        var transfer = await dbContext.StockTransfers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == id &&
                (!fiscalYearId.HasValue ||
                 entity.FiscalYearId == fiscalYearId.Value))
            .Select(entity => new
            {
                entity.Id,
                entity.CompanyId,
                entity.FiscalYearId,
                FiscalYearName = entity.FiscalYear.Name,
                entity.DocumentNumber,
                entity.TransferDate,
                entity.SourceStoreId,
                SourceStoreName = entity.SourceStore.Name,
                entity.DestinationStoreId,
                DestinationStoreName = entity.DestinationStore.Name,
                entity.Notes,
                entity.LastModifiedAt,
                entity.RowVersion,
                Lines = entity.Lines.OrderBy(line => line.Item.Name)
                    .ThenBy(line => line.ItemId)
                    .Select(line => new
                    {
                        line.Id,
                        line.ItemId,
                        ItemCode = line.Item.Code,
                        ItemName = line.Item.Name,
                        line.ItemUnitId,
                        ItemUnitName = line.ItemUnit.Name,
                        line.Quantity,
                        line.Notes
                    })
                    .ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (transfer is null)
        {
            return null;
        }

        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement => movement.CompanyId == companyId &&
                movement.FiscalYearId == transfer.FiscalYearId &&
                TransferMovementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == transfer.Id &&
                movement.ReferenceNumber == transfer.DocumentNumber)
            .Select(movement => new
            {
                movement.Id,
                movement.ItemId,
                movement.MovementType,
                movement.UnitCost,
                movement.TotalCost,
                movement.QuantityAfter,
                movement.AverageCostAfter,
                movement.InventoryValueAfter
            })
            .ToListAsync(cancellationToken);
        var byKey = movements.ToDictionary(
            movement => (movement.MovementType, movement.ItemId));

        var lines = transfer.Lines.Select(line =>
        {
            var source = byKey[(ItemMovementType.TransferOut, line.ItemId)];
            var destination = byKey[(ItemMovementType.TransferIn, line.ItemId)];
            return new StockTransferLineResponse(
                Id: line.Id,
                ItemId: line.ItemId,
                ItemCode: line.ItemCode,
                ItemName: line.ItemName,
                ItemUnitId: line.ItemUnitId,
                ItemUnitName: line.ItemUnitName,
                Quantity: line.Quantity,
                Notes: line.Notes,
                SourceMovementId: source.Id,
                SourceUnitCost: source.UnitCost ?? 0m,
                SourceTotalCost: source.TotalCost,
                SourceQuantityAfter: source.QuantityAfter,
                SourceAverageCostAfter: source.AverageCostAfter,
                SourceInventoryValueAfter: source.InventoryValueAfter,
                DestinationMovementId: destination.Id,
                DestinationUnitCost: destination.UnitCost ?? 0m,
                DestinationTotalCost: destination.TotalCost,
                DestinationQuantityAfter: destination.QuantityAfter,
                DestinationAverageCostAfter: destination.AverageCostAfter,
                DestinationInventoryValueAfter:
                    destination.InventoryValueAfter);
        }).ToArray();

        return new StockTransferResponse(
            Id: transfer.Id,
            CompanyId: transfer.CompanyId,
            FiscalYearId: transfer.FiscalYearId,
            FiscalYearName: transfer.FiscalYearName,
            DocumentNumber: transfer.DocumentNumber,
            TransferDate: transfer.TransferDate,
            SourceStoreId: transfer.SourceStoreId,
            SourceStoreName: transfer.SourceStoreName,
            DestinationStoreId: transfer.DestinationStoreId,
            DestinationStoreName: transfer.DestinationStoreName,
            Notes: transfer.Notes,
            LastModifiedAt: transfer.LastModifiedAt,
            RowVersion: transfer.RowVersion,
            Lines: lines);
    }

    private Task<StockTransfer?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.StockTransfers
            .Include(transfer => transfer.Lines)
            .SingleOrDefaultAsync(
                transfer => transfer.CompanyId == companyId &&
                    transfer.Id == id,
                cancellationToken);

    private Task<List<ItemMovement>> LoadMovementsAsync(
        int transferId,
        string documentNumber,
        CancellationToken cancellationToken) =>
        dbContext.ItemMovements
            .Where(movement => movement.CompanyId == companyId &&
                TransferMovementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == transferId &&
                movement.ReferenceNumber == documentNumber)
            .ToListAsync(cancellationToken);

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        IEnumerable<ItemMovement> movements) =>
        movements.Select(movement =>
                new InventoryCostingKey(
                    movement.StoreId,
                    movement.ItemId,
                    movement.FiscalYearId))
            .Distinct()
            .ToArray();

    private static InventoryMovementReference MovementReference(
        ItemMovementType type,
        int transferId,
        string documentNumber) =>
        new([type], transferId, documentNumber);

    private static Error? ValidateFilters(StockTransferFilterRequest filters)
    {
        if (filters.FiscalYearId is <= 0 ||
            filters.Search?.Trim().Length >
                StockTransferFilterRequest.SearchMaximumLength ||
            filters.SourceStoreId is <= 0 ||
            filters.DestinationStoreId is <= 0 ||
            filters.ItemId is <= 0 ||
            filters.ToDate < filters.FromDate)
        {
            return FiltersInvalid();
        }

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsDocumentNumberConflict(DbUpdateException exception) =>
        exception.ToString().Contains(
            "IX_StockTransfers_CompanyId_DocumentNumber",
            StringComparison.OrdinalIgnoreCase);

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId,
        bool IsActive,
        bool ItemUnitIsActive);
}
