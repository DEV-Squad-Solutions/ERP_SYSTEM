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

public sealed class StockTransferService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService,
    TimeProvider timeProvider)
    : IStockTransferService, IScopedService
{
    private static readonly ItemMovementType[] TransferMovementTypes =
    [
        ItemMovementType.TransferOut,
        ItemMovementType.TransferIn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StockTransferListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            StockTransferFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        if (ValidateFilters(filters) is { } filterError)
        {
            return Result<PagedResponse<StockTransferListResponse>>.Failure(
                filterError);
        }

        var search = filters.Search?.Trim();
        var query = dbContext.StockTransfers
            .AsNoTracking()
            .Where(transfer => transfer.CompanyId == companyId)
            .Where(transfer => string.IsNullOrEmpty(search) ||
                transfer.DocumentNumber.Contains(search) ||
                transfer.SourceStore.Name.Contains(search) ||
                transfer.DestinationStore.Name.Contains(search))
            .Where(transfer => !filters.SourceStoreId.HasValue ||
                transfer.SourceStoreId == filters.SourceStoreId.Value)
            .Where(transfer => !filters.DestinationStoreId.HasValue ||
                transfer.DestinationStoreId == filters.DestinationStoreId.Value)
            .Where(transfer => !filters.ItemId.HasValue ||
                transfer.Lines.Any(line => line.ItemId == filters.ItemId.Value))
            .Where(transfer => !filters.FromDate.HasValue ||
                transfer.TransferDate >= filters.FromDate.Value)
            .Where(transfer => !filters.ToDate.HasValue ||
                transfer.TransferDate <= filters.ToDate.Value)
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.Id);

        return await paginationService.PaginateAsync<
            StockTransfer,
            StockTransferListResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<StockTransferResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockTransferResponse>.Failure(InvalidId());
        }

        var response = await BuildResponseAsync(id, cancellationToken);
        return response is null
            ? Result<StockTransferResponse>.Failure(NotFound(id))
            : Result<StockTransferResponse>.Success(response);
    }

    public async Task<Result<StockTransferResponse>> AddAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var shapeError = ValidateRequestShape(
            request.DocumentNumber,
            request.TransferDate,
            request.SourceStoreId,
            request.DestinationStoreId,
            request.Lines);
        if (shapeError is not null)
        {
            return Result<StockTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var preparation = await PrepareAsync(
            request.SourceStoreId,
            request.DestinationStoreId,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(preparation.Error);
        }

        var documentNumber = request.DocumentNumber.Trim();
        if (await DocumentNumberExistsAsync(documentNumber, cancellationToken))
        {
            return Result<StockTransferResponse>.Failure(
                DocumentNumberExists(documentNumber));
        }

        var sourceKeys = request.Lines
            .Select(line => new InventoryCostingKey(
                request.SourceStoreId,
                line.ItemId))
            .ToArray();
        var destinationKeys = request.Lines
            .Select(line => new InventoryCostingKey(
                request.DestinationStoreId,
                line.ItemId))
            .ToArray();
        await inventoryCostingService.LockAsync(
            sourceKeys.Concat(destinationKeys).ToArray(),
            cancellationToken);

        var stockError = await ValidateStockAsync(
            request.SourceStoreId,
            request.TransferDate,
            request.Lines,
            replacedMovement: null,
            $"إضافة تحويل المخزون {documentNumber}",
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockTransferResponse>.Failure(stockError);
        }

        var transfer = request.Adapt<StockTransfer>();
        transfer.CompanyId = companyId;
        transfer.Touch(timeProvider.GetUtcNow().UtcDateTime);
        AddLines(transfer, request.Lines, preparation.Value);
        dbContext.StockTransfers.Add(transfer);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            AddMovements(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await inventoryCostingService.RecalculateAsync(
                sourceKeys,
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDocumentNumberConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockTransferResponse>.Failure(
                DocumentNumberExists(documentNumber));
        }

        var response = await BuildResponseAsync(transfer.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<StockTransferResponse>.Success(response!);
    }

    public async Task<Result<StockTransferResponse>> UpdateAsync(
        int id,
        StockTransferUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockTransferResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<StockTransferResponse>.Failure(RowVersionRequired());
        }

        var shapeError = ValidateLines(request.TransferDate, request.Lines);
        if (shapeError is not null)
        {
            return Result<StockTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var transfer = await LoadForWriteAsync(id, cancellationToken);
        if (transfer is null)
        {
            return Result<StockTransferResponse>.Failure(NotFound(id));
        }

        var preparation = await PrepareAsync(
            transfer.SourceStoreId,
            transfer.DestinationStoreId,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(preparation.Error);
        }

        var movements = await LoadMovementsAsync(
            transfer.Id,
            transfer.DocumentNumber,
            cancellationToken);
        var oldKeys = GetCostingKeys(movements);
        var newSourceKeys = request.Lines.Select(line =>
            new InventoryCostingKey(transfer.SourceStoreId, line.ItemId)).ToArray();
        var newDestinationKeys = request.Lines.Select(line =>
            new InventoryCostingKey(transfer.DestinationStoreId, line.ItemId)).ToArray();
        await inventoryCostingService.LockAsync(
            oldKeys.Concat(newSourceKeys).Concat(newDestinationKeys).ToArray(),
            cancellationToken);

        var sourceStockError = await ValidateStockAsync(
            transfer.SourceStoreId,
            request.TransferDate,
            request.Lines,
            MovementReference(
                ItemMovementType.TransferOut,
                transfer.Id,
                transfer.DocumentNumber),
            $"تعديل تحويل المخزون {transfer.DocumentNumber}",
            cancellationToken);
        if (sourceStockError is not null)
        {
            return Result<StockTransferResponse>.Failure(sourceStockError);
        }

        var destinationStockError = await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: transfer.DestinationStoreId,
                MovementDate: request.TransferDate,
                IsInbound: true,
                Lines: request.Lines.Select(line =>
                    new InventoryStockLine(line.ItemId, line.Quantity)).ToArray(),
                ReplacedMovement: MovementReference(
                    ItemMovementType.TransferIn,
                    transfer.Id,
                    transfer.DocumentNumber),
                OperationDescription:
                    $"تعديل تحويل المخزون {transfer.DocumentNumber}",
                ErrorFieldName: nameof(StockTransferRequest.Lines)),
            cancellationToken);
        if (destinationStockError is not null)
        {
            return Result<StockTransferResponse>.Failure(destinationStockError);
        }

        dbContext.Entry(transfer).Property(entity => entity.RowVersion)
            .OriginalValue = request.RowVersion;
        request.Adapt(transfer);
        transfer.Touch(timeProvider.GetUtcNow().UtcDateTime);
        ReplaceLines(transfer, request.Lines, preparation.Value);
        ReconcileMovements(transfer, movements);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var sourceCostingError = await inventoryCostingService.RecalculateAsync(
                oldKeys.Where(key => key.StoreId == transfer.SourceStoreId)
                    .Concat(newSourceKeys)
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (sourceCostingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(sourceCostingError);
            }

            var destinationCostingError = await inventoryCostingService
                .RecalculateAsync(
                    oldKeys.Where(key =>
                            key.StoreId == transfer.DestinationStoreId)
                        .Concat(newDestinationKeys)
                        .Distinct()
                        .ToArray(),
                    cancellationToken);
            if (destinationCostingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(
                    destinationCostingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockTransferResponse>.Failure(Concurrency());
        }

        var response = await BuildResponseAsync(transfer.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<StockTransferResponse>.Success(response!);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var transfer = await LoadForWriteAsync(id, cancellationToken);
        if (transfer is null)
        {
            return Result.Failure(NotFound(id));
        }

        var movements = await LoadMovementsAsync(
            transfer.Id,
            transfer.DocumentNumber,
            cancellationToken);
        var costingKeys = GetCostingKeys(movements);
        await inventoryCostingService.LockAsync(costingKeys, cancellationToken);

        var destinationStockError = await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: transfer.DestinationStoreId,
                MovementDate: transfer.TransferDate,
                IsInbound: true,
                Lines: [],
                ReplacedMovement: MovementReference(
                    ItemMovementType.TransferIn,
                    transfer.Id,
                    transfer.DocumentNumber),
                OperationDescription:
                    $"حذف تحويل المخزون {transfer.DocumentNumber}",
                ErrorFieldName: nameof(StockTransferRequest.Lines)),
            cancellationToken);
        if (destinationStockError is not null)
        {
            return Result.Failure(destinationStockError);
        }

        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockTransferLines.RemoveRange(transfer.Lines);
        dbContext.StockTransfers.Remove(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);

        var costingError = await inventoryCostingService.RecalculateAsync(
            costingKeys,
            cancellationToken);
        if (costingError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(costingError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

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
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && itemIds.Contains(item.Id))
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                item.ItemUnit.IsActive))
            .ToListAsync(cancellationToken);
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
        string documentNumber,
        DateOnly transferDate,
        int sourceStoreId,
        int destinationStoreId,
        IReadOnlyCollection<StockTransferLineRequest>? lines)
    {
        if (string.IsNullOrWhiteSpace(documentNumber) ||
            documentNumber.Trim().Length >
            StockTransferRequest.DocumentNumberMaximumLength)
        {
            return Error.Validation(
                "StockTransfers.DocumentNumberInvalid",
                "رقم تحويل المخزون مطلوب ويجب ألا يتجاوز 50 حرفاً.",
                nameof(StockTransferRequest.DocumentNumber));
        }

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
                ErrorFieldName: nameof(StockTransferRequest.Lines)),
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
        CancellationToken cancellationToken)
    {
        var transfer = await dbContext.StockTransfers
            .AsNoTracking()
            .Where(entity => entity.CompanyId == companyId && entity.Id == id)
            .Select(entity => new
            {
                entity.Id,
                entity.CompanyId,
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

    private Task<bool> DocumentNumberExistsAsync(
        string documentNumber,
        CancellationToken cancellationToken) =>
        dbContext.StockTransfers.AnyAsync(
            transfer => transfer.CompanyId == companyId &&
                transfer.DocumentNumber == documentNumber,
            cancellationToken);

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        IEnumerable<ItemMovement> movements) =>
        movements.Select(movement =>
                new InventoryCostingKey(movement.StoreId, movement.ItemId))
            .Distinct()
            .ToArray();

    private static InventoryMovementReference MovementReference(
        ItemMovementType type,
        int transferId,
        string documentNumber) =>
        new([type], transferId, documentNumber);

    private static Error? ValidateFilters(StockTransferFilterRequest filters)
    {
        if (filters.Search?.Trim().Length >
                StockTransferRequest.DocumentNumberMaximumLength ||
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
