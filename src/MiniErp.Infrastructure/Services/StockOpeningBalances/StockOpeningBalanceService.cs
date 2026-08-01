using System.Data;
using static MiniErp.Application.Features.StockOpeningBalances.StockOpeningBalanceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockOpeningBalances;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockOpeningBalances;

public sealed class StockOpeningBalanceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryCostingService inventoryCostingService,
    IInventoryStockService inventoryStockService)
    : IStockOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StockOpeningBalanceListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StockOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new StockOpeningBalanceFilterRequest();
        var query = dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .Where(balance =>
                string.IsNullOrWhiteSpace(filters.DocumentNumber) ||
                balance.DocumentNumber.Contains(filters.DocumentNumber.Trim()))
            .Where(balance =>
                !filters.StoreId.HasValue ||
                balance.StoreId == filters.StoreId.Value)
            .Where(balance =>
                !filters.FromDate.HasValue ||
                balance.DocumentDate >= filters.FromDate.Value)
            .Where(balance =>
                !filters.ToDate.HasValue ||
                balance.DocumentDate <= filters.ToDate.Value)
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.Id);

        var pageResult = await paginationService.PaginateAsync<
            StockOpeningBalance,
            StockOpeningBalanceListResponse>(
                query,
                pagination,
                cancellationToken);
        if (pageResult.IsFailure)
        {
            return pageResult;
        }

        var page = pageResult.Value;
        var enrichedItems = await EnrichListResponsesAsync(
            page.Items,
            cancellationToken);

        return Result<PagedResponse<StockOpeningBalanceListResponse>>.Success(
            page with { Items = enrichedItems });
    }

    public async Task<Result<StockOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockOpeningBalanceResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (response is not null)
        {
            response = await EnrichResponseAsync(
                response,
                cancellationToken);
        }

        return response is null
            ? Result<StockOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<StockOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<StockOpeningBalanceResponse>> AddAsync(
        StockOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var normalized = request.Adapt<StockOpeningBalance>();

        var validationResult = await ValidateRequestAsync(
            normalized.StoreId,
            request.Lines,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                validationResult.Error);
        }

        await inventoryCostingService.LockAsync(
            request.Lines
                .Select(line => new InventoryCostingKey(
                    normalized.StoreId,
                    line.ItemId))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var documentNumberExists = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        var openingBalance = new StockOpeningBalance
        {
            CompanyId = companyId,
            StoreId = normalized.StoreId,
            DocumentNumber = normalized.DocumentNumber,
            DocumentDate = normalized.DocumentDate,
            Notes = normalized.Notes
        };

        AddLines(openingBalance, request.Lines, validationResult.Value);
        dbContext.StockOpeningBalances.Add(openingBalance);
        await dbContext.SaveChangesAsync(cancellationToken);

        AddMovements(openingBalance);
        await dbContext.SaveChangesAsync(cancellationToken);

        var costingError = await inventoryCostingService.RecalculateAsync(
            GetCostingKeys(openingBalance),
            cancellationToken);
        if (costingError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockOpeningBalanceResponse>.Failure(costingError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(openingBalance.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await EnrichResponseAsync(response, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<StockOpeningBalanceResponse>> UpdateAsync(
        int id,
        StockOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockOpeningBalanceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: > 0 })
        {
            return Result<StockOpeningBalanceResponse>.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var normalized = request.Adapt<StockOpeningBalance>();

        var openingBalance = await LoadForWriteAsync(id, cancellationToken);
        if (openingBalance is null)
        {
            return Result<StockOpeningBalanceResponse>.Failure(NotFound(id));
        }

        if (!openingBalance.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<StockOpeningBalanceResponse>.Failure(Concurrency());
        }

        var oldMovements = await LoadMovementsAsync(id, cancellationToken);
        var oldCostingKeys = GetCostingKeys(oldMovements);
        await inventoryCostingService.LockAsync(
            oldCostingKeys
                .Concat(request.Lines.Select(line =>
                    new InventoryCostingKey(
                        normalized.StoreId,
                        line.ItemId)))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var stockError = await ValidateStockAsync(
            normalized.StoreId,
            normalized.DocumentDate,
            request.Lines,
            openingBalance.Id,
            openingBalance.DocumentNumber,
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockOpeningBalanceResponse>.Failure(stockError);
        }

        var validationResult = await ValidateRequestAsync(
            normalized.StoreId,
            request.Lines,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                validationResult.Error);
        }

        var documentNumberExists = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.Id != id &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        openingBalance.StoreId = normalized.StoreId;
        openingBalance.DocumentNumber = normalized.DocumentNumber;
        openingBalance.DocumentDate = normalized.DocumentDate;
        openingBalance.Notes = normalized.Notes;

        ReplaceLines(
            openingBalance,
            request.Lines,
            validationResult.Value,
            dbContext);

        var openingBalanceEntry = dbContext.Entry(openingBalance);
        openingBalanceEntry.State = EntityState.Modified;
        openingBalanceEntry.Property(balance => balance.RowVersion)
            .OriginalValue = request.RowVersion;
        ReconcileMovements(openingBalance, oldMovements);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await inventoryCostingService.RecalculateAsync(
                oldCostingKeys
                    .Concat(GetCostingKeys(openingBalance))
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockOpeningBalanceResponse>.Failure(
                    costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<StockOpeningBalanceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await EnrichResponseAsync(response, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockOpeningBalanceResponse>.Success(response);
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
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var openingBalance = await LoadForWriteAsync(id, cancellationToken);
        if (openingBalance is null)
        {
            return Result.Failure(NotFound(id));
        }

        var movements = await LoadMovementsAsync(id, cancellationToken);
        var costingKeys = GetCostingKeys(movements);
        await inventoryCostingService.LockAsync(
            costingKeys,
            cancellationToken);
        var stockError = await ValidateStockAsync(
            openingBalance.StoreId,
            openingBalance.DocumentDate,
            [],
            openingBalance.Id,
            openingBalance.DocumentNumber,
            cancellationToken);
        if (stockError is not null)
        {
            return Result.Failure(stockError);
        }

        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockOpeningBalanceLines.RemoveRange(openingBalance.Lines);
        dbContext.StockOpeningBalances.Remove(openingBalance);

        try
        {
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
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<StockOpeningBalanceResponse> ProjectResponseQuery(int id) =>
        dbContext.StockOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id)
            .ProjectToType<StockOpeningBalanceResponse>();

    private async Task<IReadOnlyList<StockOpeningBalanceListResponse>>
        EnrichListResponsesAsync(
            IReadOnlyList<StockOpeningBalanceListResponse> responses,
            CancellationToken cancellationToken)
    {
        var costs = await LoadMovementCostsAsync(
            responses.Select(response => response.Id).ToArray(),
            cancellationToken);

        return responses
            .Select(response => response with
            {
                Lines = response.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => ApplyCost(
                        line,
                        costs.GetValueOrDefault(
                            (response.Id, line.ItemId))))
                    .ToArray()
            })
            .ToArray();
    }

    private async Task<StockOpeningBalanceResponse> EnrichResponseAsync(
        StockOpeningBalanceResponse response,
        CancellationToken cancellationToken)
    {
        var costs = await LoadMovementCostsAsync(
            [response.Id],
            cancellationToken);

        return response with
        {
            Lines = response.Lines
                .OrderBy(line => line.Id)
                .Select(line => ApplyCost(
                    line,
                    costs.GetValueOrDefault(
                        (response.Id, line.ItemId))))
                .ToArray()
        };
    }

    private async Task<Dictionary<(int ReferenceId, int ItemId),
        OpeningMovementCost>> LoadMovementCostsAsync(
        IReadOnlyCollection<int> referenceIds,
        CancellationToken cancellationToken)
    {
        if (referenceIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementType == ItemMovementType.OpeningBalance &&
                referenceIds.Contains(movement.ReferenceId))
            .ToDictionaryAsync(
                movement => (movement.ReferenceId, movement.ItemId),
                movement => new OpeningMovementCost(
                    movement.CostStatus,
                    movement.UnitCost,
                    movement.TotalCost,
                    movement.QuantityAfter,
                    movement.AverageCostAfter,
                    movement.InventoryValueAfter),
                cancellationToken);
    }

    private static StockOpeningBalanceLineResponse ApplyCost(
        StockOpeningBalanceLineResponse line,
        OpeningMovementCost? cost) =>
        cost is null
            ? line
            : line with
            {
                CostStatus = cost.CostStatus,
                UnitCost = cost.UnitCost,
                InventoryTotalCost = cost.TotalCost,
                QuantityAfter = cost.QuantityAfter,
                AverageCostAfter = cost.AverageCostAfter,
                InventoryValueAfter = cost.InventoryValueAfter
            };

    private Task<StockOpeningBalance?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.StockOpeningBalances
            .Include(balance => balance.Lines.Where(line =>
                line.CompanyId == companyId))
            .FirstOrDefaultAsync(
                balance => balance.CompanyId == companyId && balance.Id == id,
                cancellationToken);

    private static void AddLines(
        StockOpeningBalance openingBalance,
        IReadOnlyList<StockOpeningBalanceLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        foreach (var lineRequest in requests)
        {
            var item = items[lineRequest.ItemId];
            var line = lineRequest.Adapt<StockOpeningBalanceLine>();
            line.CompanyId = openingBalance.CompanyId;
            line.ItemId = item.Id;
            line.ItemUnitId = item.ItemUnitId;
            line.StockOpeningBalance = openingBalance;
            line.CalculateAmounts();
            openingBalance.Lines.Add(line);
        }
    }

    private static void ReplaceLines(
        StockOpeningBalance openingBalance,
        IReadOnlyList<StockOpeningBalanceLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items,
        ApplicationDbContext dbContext)
    {
        var requestedItemIds = requests
            .Select(request => request.ItemId)
            .ToHashSet();
        var existingLinesByItemId = openingBalance.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var line in openingBalance.Lines
                     .Where(line => !requestedItemIds.Contains(line.ItemId))
                     .ToList())
        {
            dbContext.StockOpeningBalanceLines.Remove(line);
            openingBalance.Lines.Remove(line);
        }

        foreach (var lineRequest in requests)
        {
            var item = items[lineRequest.ItemId];
            if (existingLinesByItemId.TryGetValue(
                    lineRequest.ItemId,
                    out var existingLine))
            {
                var normalizedLine = lineRequest.Adapt<StockOpeningBalanceLine>();
                existingLine.ItemUnitId = item.ItemUnitId;
                existingLine.Count = normalizedLine.Count;
                existingLine.Weight = normalizedLine.Weight;
                existingLine.Price = normalizedLine.Price;
                existingLine.Notes = normalizedLine.Notes;
                existingLine.CalculateAmounts();
                continue;
            }

            var line = lineRequest.Adapt<StockOpeningBalanceLine>();
            line.CompanyId = openingBalance.CompanyId;
            line.ItemId = item.Id;
            line.ItemUnitId = item.ItemUnitId;
            line.StockOpeningBalance = openingBalance;
            line.CalculateAmounts();
            openingBalance.Lines.Add(line);
        }
    }

    private void AddMovements(StockOpeningBalance openingBalance)
    {
        foreach (var line in openingBalance.Lines.Where(line =>
                     !line.IsDeleted))
        {
            dbContext.ItemMovements.Add(
                CreateMovement(openingBalance, line));
        }
    }

    private void ReconcileMovements(
        StockOpeningBalance openingBalance,
        IReadOnlyCollection<ItemMovement> existingMovements)
    {
        var activeLines = openingBalance.Lines
            .Where(line => !line.IsDeleted)
            .ToDictionary(line => line.ItemId);
        var existingItemIds = new HashSet<int>();

        foreach (var movement in existingMovements)
        {
            if (!activeLines.TryGetValue(movement.ItemId, out var line))
            {
                dbContext.ItemMovements.Remove(movement);
                continue;
            }

            existingItemIds.Add(line.ItemId);
            movement.StoreId = openingBalance.StoreId;
            movement.ItemUnitId = line.ItemUnitId;
            movement.ReferenceNumber = openingBalance.DocumentNumber;
            movement.MovementDate = openingBalance.DocumentDate;
            movement.QuantityIn = line.Quantity;
            movement.QuantityOut = 0m;
            movement.Description =
                $"Opening balance {openingBalance.DocumentNumber}";
        }

        foreach (var line in activeLines.Values.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            dbContext.ItemMovements.Add(
                CreateMovement(openingBalance, line));
        }
    }

    private ItemMovement CreateMovement(
        StockOpeningBalance openingBalance,
        StockOpeningBalanceLine line) =>
        new()
        {
            CompanyId = companyId,
            StoreId = openingBalance.StoreId,
            ItemId = line.ItemId,
            ItemUnitId = line.ItemUnitId,
            MovementType = ItemMovementType.OpeningBalance,
            ReferenceId = openingBalance.Id,
            ReferenceNumber = openingBalance.DocumentNumber,
            MovementDate = openingBalance.DocumentDate,
            QuantityIn = line.Quantity,
            QuantityOut = 0m,
            Description =
                $"Opening balance {openingBalance.DocumentNumber}"
        };

    private Task<List<ItemMovement>> LoadMovementsAsync(
        int openingBalanceId,
        CancellationToken cancellationToken) =>
        dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementType ==
                    ItemMovementType.OpeningBalance &&
                movement.ReferenceId == openingBalanceId)
            .ToListAsync(cancellationToken);

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        StockOpeningBalance openingBalance) =>
        openingBalance.Lines
            .Where(line => !line.IsDeleted)
            .Select(line => new InventoryCostingKey(
                openingBalance.StoreId,
                line.ItemId))
            .Distinct()
            .ToArray();

    private Task<Error?> ValidateStockAsync(
        int storeId,
        DateOnly movementDate,
        IReadOnlyCollection<StockOpeningBalanceLineRequest> lines,
        int openingBalanceId,
        string documentNumber,
        CancellationToken cancellationToken) =>
        inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                storeId,
                movementDate,
                IsInbound: true,
                lines.Select(line =>
                    new InventoryStockLine(
                        line.ItemId,
                        line.Count * line.Weight))
                    .ToArray(),
                new InventoryMovementReference(
                    [ItemMovementType.OpeningBalance],
                    openingBalanceId,
                    documentNumber),
                $"تعديل أو حذف الرصيد الافتتاحي {documentNumber}",
                nameof(StockOpeningBalanceRequest.Lines)),
            cancellationToken);

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        IEnumerable<ItemMovement> movements) =>
        movements
            .Select(movement => new InventoryCostingKey(
                movement.StoreId,
                movement.ItemId))
            .Distinct()
            .ToArray();

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateRequestAsync(
            int storeId,
            IReadOnlyList<StockOpeningBalanceLineRequest> lines,
            CancellationToken cancellationToken)
    {
        var storeError = await ValidateStoreAsync(
            storeId,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(storeError);
        }

        return await ValidateLineItemsAsync(
            [.. lines.Select(line => line.ItemId)],
            cancellationToken);
    }

    private async Task<Error?> ValidateStoreAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == storeId)
            .Select(entity => new
            {
                entity.IsActive,
                entity.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (store is null)
        {
            return StoreNotFound(storeId);
        }

        if (store.IsContainerStore)
        {
            return ContainerStoreNotAllowed();
        }

        return store.IsActive
            ? null
            : StoreInactive();
    }

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateLineItemsAsync(
            IReadOnlyCollection<int> itemIds,
            CancellationToken cancellationToken)
    {
        var distinctItemIds = itemIds.Distinct().ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                distinctItemIds.Contains(item.Id))
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                item.ItemUnit.IsActive))
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(item => item.Id);
        var missingIds = distinctItemIds
            .Where(itemId => !itemsById.ContainsKey(itemId))
            .ToArray();
        if (missingIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(ItemNotFound(missingIds));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(ItemInactive(inactiveItemIds));
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

    private sealed record OpeningMovementCost(
        InventoryCostStatus CostStatus,
        decimal? UnitCost,
        decimal TotalCost,
        decimal QuantityAfter,
        decimal AverageCostAfter,
        decimal InventoryValueAfter);

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId,
        bool IsActive,
        bool ItemUnitIsActive);
}
