using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed class InventoryStockService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IInventoryStockService
{
    private readonly int companyId = currentCompanyContext.CompanyId;
    private StockBalanceCheckMode? balanceCheckMode;

    public async Task<IReadOnlyDictionary<int, decimal>> GetBalancesAsync(
        int storeId,
        IReadOnlyCollection<int> itemIds,
        DateOnly asOfDate,
        InventoryMovementReference? excludedMovement = null,
        CancellationToken cancellationToken = default)
    {
        var distinctItemIds = itemIds.Distinct().ToArray();
        if (distinctItemIds.Length == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var legacyOpeningBalances = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                distinctItemIds.Contains(line.ItemId) &&
                line.StockOpeningBalance.CompanyId == companyId &&
                line.StockOpeningBalance.StoreId == storeId &&
                line.StockOpeningBalance.DocumentDate <= asOfDate &&
                !dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.StoreId == storeId &&
                    movement.ItemId == line.ItemId &&
                    movement.MovementType ==
                        ItemMovementType.OpeningBalance &&
                    movement.ReferenceId ==
                        line.StockOpeningBalanceId))
            .GroupBy(line => line.ItemId)
            .Select(group => new
            {
                ItemId = group.Key,
                Quantity = group.Sum(line => line.Quantity)
            })
            .ToDictionaryAsync(
                item => item.ItemId,
                item => item.Quantity,
                cancellationToken);

        var movementQuery = dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.StoreId == storeId &&
                distinctItemIds.Contains(movement.ItemId) &&
                movement.MovementDate <= asOfDate);

        movementQuery = ExcludeMovement(
            movementQuery,
            excludedMovement);

        var movementBalances = await movementQuery
            .GroupBy(movement => movement.ItemId)
            .Select(group => new
            {
                ItemId = group.Key,
                Quantity = group.Sum(movement =>
                    movement.QuantityIn - movement.QuantityOut)
            })
            .ToDictionaryAsync(
                item => item.ItemId,
                item => item.Quantity,
                cancellationToken);

        return distinctItemIds.ToDictionary(
            itemId => itemId,
            itemId =>
                legacyOpeningBalances.GetValueOrDefault(itemId) +
                movementBalances.GetValueOrDefault(itemId));
    }

    public async Task<Error?> ValidateTimelineAsync(
        InventoryStockProposal proposal,
        CancellationToken cancellationToken = default)
    {
        if (proposal.IsInbound && proposal.ReplacedMovement is null)
        {
            return null;
        }

        var mode = await GetBalanceCheckModeAsync(cancellationToken);
        if (mode == StockBalanceCheckMode.None)
        {
            return null;
        }

        if (mode is StockBalanceCheckMode.DateCheck or StockBalanceCheckMode.Both)
        {
            var dateError = await ValidateDateBalanceAsync(
                proposal,
                cancellationToken);
            if (dateError is not null)
            {
                return dateError;
            }
        }

        return mode is StockBalanceCheckMode.FinalCheck or StockBalanceCheckMode.Both
            ? await ValidateFinalBalanceAsync(proposal, cancellationToken)
            : null;
    }

    private async Task<Error?> ValidateDateBalanceAsync(
        InventoryStockProposal proposal,
        CancellationToken cancellationToken)
    {
        var requestedByItem = proposal.Lines
            .GroupBy(line => line.ItemId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity));

        var currentMovements = await LoadCurrentMovementKeysAsync(
            proposal.ReplacedMovement,
            cancellationToken);

        var affectedStockKeys = currentMovements.ToHashSet();
        foreach (var itemId in requestedByItem.Keys)
        {
            affectedStockKeys.Add((proposal.StoreId, itemId));
        }

        if (affectedStockKeys.Count == 0)
        {
            return null;
        }

        var storeIds = affectedStockKeys
            .Select(key => key.StoreId)
            .Distinct()
            .ToArray();
        var itemIds = affectedStockKeys
            .Select(key => key.ItemId)
            .Distinct()
            .ToArray();

        var itemNames = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name,
                cancellationToken);

        var movementQuery = dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                storeIds.Contains(movement.StoreId) &&
                itemIds.Contains(movement.ItemId));

        movementQuery = ExcludeMovement(
            movementQuery,
            proposal.ReplacedMovement);

        var movements = await movementQuery
            .Select(movement => new
            {
                movement.Id,
                movement.StoreId,
                movement.ItemId,
                movement.MovementDate,
                movement.QuantityIn,
                movement.QuantityOut
            })
            .ToListAsync(cancellationToken);

        var legacyOpeningBalances = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.CompanyId == companyId &&
                storeIds.Contains(line.StockOpeningBalance.StoreId) &&
                itemIds.Contains(line.ItemId) &&
                !dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.StoreId ==
                        line.StockOpeningBalance.StoreId &&
                    movement.ItemId == line.ItemId &&
                    movement.MovementType ==
                        ItemMovementType.OpeningBalance &&
                    movement.ReferenceId ==
                        line.StockOpeningBalanceId))
            .Select(line => new
            {
                line.Id,
                StoreId = line.StockOpeningBalance.StoreId,
                line.ItemId,
                Date = line.StockOpeningBalance.DocumentDate,
                line.Quantity
            })
            .ToListAsync(cancellationToken);

        foreach (var (storeId, itemId) in affectedStockKeys)
        {
            var itemName = itemNames.GetValueOrDefault(itemId) ??
                itemId.ToString();
            var requestedQuantity =
                storeId == proposal.StoreId
                    ? requestedByItem.GetValueOrDefault(itemId)
                    : 0m;
            var events = new List<StockEvent>();

            events.AddRange(
                legacyOpeningBalances
                    .Where(line =>
                        line.StoreId == storeId &&
                        line.ItemId == itemId)
                    .Select(line => new StockEvent(
                        line.Date,
                        Priority: 0,
                        line.Id,
                        QuantityIn: line.Quantity,
                        QuantityOut: 0m,
                        IsProposed: false)));

            foreach (var movement in movements.Where(
                         movement =>
                             movement.StoreId == storeId &&
                             movement.ItemId == itemId))
            {
                if (movement.QuantityIn > 0m)
                {
                    events.Add(
                        new StockEvent(
                            movement.MovementDate,
                            Priority: 1,
                            movement.Id,
                            movement.QuantityIn,
                            QuantityOut: 0m,
                            IsProposed: false));
                }

                if (movement.QuantityOut > 0m)
                {
                    events.Add(
                        new StockEvent(
                            movement.MovementDate,
                            Priority: 2,
                            movement.Id,
                            QuantityIn: 0m,
                            movement.QuantityOut,
                            IsProposed: false));
                }
            }

            if (storeId == proposal.StoreId &&
                requestedQuantity > 0m)
            {
                events.Add(
                    new StockEvent(
                        proposal.MovementDate,
                        Priority: proposal.IsInbound ? 1 : 2,
                        Id: int.MaxValue,
                        QuantityIn: proposal.IsInbound
                            ? requestedQuantity
                            : 0m,
                        QuantityOut: proposal.IsInbound
                            ? 0m
                            : requestedQuantity,
                        IsProposed: true));
            }

            var balance = 0m;
            foreach (var stockEvent in events
                         .OrderBy(stockEvent => stockEvent.Date)
                         .ThenBy(stockEvent => stockEvent.Priority)
                         .ThenBy(stockEvent => stockEvent.Id))
            {
                var availableBeforeMovement = balance;
                balance +=
                    stockEvent.QuantityIn -
                    stockEvent.QuantityOut;
                if (balance >= 0m)
                {
                    continue;
                }

                if (stockEvent.IsProposed &&
                    stockEvent.QuantityOut > 0m)
                {
                    return InventoryErrors.InsufficientStockAtDate(
                        itemName,
                        itemId,
                        storeId,
                        stockEvent.Date,
                        availableBeforeMovement,
                        stockEvent.QuantityOut,
                        proposal.ErrorFieldName);
                }

                return InventoryErrors.HistoricalStockConflict(
                    proposal.OperationDescription,
                    proposal.MovementDate,
                    itemName,
                    itemId,
                    storeId,
                    stockEvent.Date,
                    availableBeforeMovement,
                    stockEvent.QuantityOut,
                    proposal.ErrorFieldName);
            }
        }

        return null;
    }

    private async Task<Error?> ValidateFinalBalanceAsync(
        InventoryStockProposal proposal,
        CancellationToken cancellationToken)
    {
        var requestedByItem = proposal.Lines
            .GroupBy(line => line.ItemId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity));

        var currentMovements = await LoadCurrentMovementKeysAsync(
            proposal.ReplacedMovement,
            cancellationToken);
        var affectedStockKeys = currentMovements.ToHashSet();
        foreach (var itemId in requestedByItem.Keys)
        {
            affectedStockKeys.Add((proposal.StoreId, itemId));
        }

        if (affectedStockKeys.Count == 0)
        {
            return null;
        }

        var itemIds = affectedStockKeys
            .Select(key => key.ItemId)
            .Distinct()
            .ToArray();
        var itemNames = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name,
                cancellationToken);

        foreach (var storeId in affectedStockKeys
                     .Select(key => key.StoreId)
                     .Distinct())
        {
            var storeItemIds = affectedStockKeys
                .Where(key => key.StoreId == storeId)
                .Select(key => key.ItemId)
                .Distinct()
                .ToArray();
            var balances = await GetBalancesAsync(
                storeId,
                storeItemIds,
                DateOnly.MaxValue,
                proposal.ReplacedMovement,
                cancellationToken);

            foreach (var itemId in storeItemIds)
            {
                var requestedQuantity =
                    storeId == proposal.StoreId
                        ? requestedByItem.GetValueOrDefault(itemId)
                        : 0m;
                var proposedDelta = proposal.IsInbound
                    ? requestedQuantity
                    : -requestedQuantity;
                var finalBalance =
                    balances.GetValueOrDefault(itemId) + proposedDelta;
                if (finalBalance >= 0m)
                {
                    continue;
                }

                var itemName = itemNames.GetValueOrDefault(itemId) ??
                    itemId.ToString();
                if (!proposal.IsInbound && requestedQuantity > 0m &&
                    storeId == proposal.StoreId)
                {
                    return InventoryErrors.InsufficientFinalStock(
                        itemName,
                        itemId,
                        storeId,
                        finalBalance,
                        proposal.ErrorFieldName);
                }

                return InventoryErrors.HistoricalFinalStockConflict(
                    itemName,
                    itemId,
                    storeId,
                    proposal.ErrorFieldName);
            }
        }

        return null;
    }

    private async Task<StockBalanceCheckMode> GetBalanceCheckModeAsync(
        CancellationToken cancellationToken)
    {
        if (balanceCheckMode.HasValue)
        {
            return balanceCheckMode.Value;
        }

        balanceCheckMode = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (StockBalanceCheckMode?)settings.StockBalanceCheckMode)
            .SingleOrDefaultAsync(cancellationToken) ??
            StockBalanceCheckMode.DateCheck;
        return balanceCheckMode.Value;
    }

    public async Task<bool> HasStockChangesSinceAsync(
        int storeId,
        IReadOnlyCollection<int> itemIds,
        DateTime snapshotTakenAt,
        CancellationToken cancellationToken = default)
    {
        var distinctItemIds = itemIds.Distinct().ToArray();
        if (distinctItemIds.Length == 0)
        {
            return false;
        }

        var movementChanged = await dbContext.ItemMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.StoreId == storeId &&
                    distinctItemIds.Contains(movement.ItemId) &&
                    (
                        movement.CreatedOn > snapshotTakenAt ||
                        movement.UpdatedOn > snapshotTakenAt ||
                        movement.DeletedOn > snapshotTakenAt
                    ),
                cancellationToken);
        if (movementChanged)
        {
            return true;
        }

        return await dbContext.StockOpeningBalanceLines
            .IgnoreQueryFilters()
            .AnyAsync(
                line =>
                    line.CompanyId == companyId &&
                    distinctItemIds.Contains(line.ItemId) &&
                    line.StockOpeningBalance.CompanyId == companyId &&
                    line.StockOpeningBalance.StoreId == storeId &&
                    (
                        line.CreatedOn > snapshotTakenAt ||
                        line.UpdatedOn > snapshotTakenAt ||
                        line.DeletedOn > snapshotTakenAt ||
                        line.StockOpeningBalance.CreatedOn > snapshotTakenAt ||
                        line.StockOpeningBalance.UpdatedOn > snapshotTakenAt ||
                        line.StockOpeningBalance.DeletedOn > snapshotTakenAt
                    ),
                cancellationToken);
    }

    private async Task<IReadOnlyCollection<(int StoreId, int ItemId)>>
        LoadCurrentMovementKeysAsync(
            InventoryMovementReference? movementReference,
            CancellationToken cancellationToken)
    {
        if (movementReference is null)
        {
            return [];
        }

        var movementTypes = movementReference.MovementTypes
            .Distinct()
            .ToArray();

        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == movementReference.ReferenceId &&
                movement.ReferenceNumber == movementReference.ReferenceNumber)
            .Select(movement => new
            {
                movement.StoreId,
                movement.ItemId
            })
            .ToListAsync(cancellationToken);

        return movements
            .Select(movement => (movement.StoreId, movement.ItemId))
            .ToArray();
    }

    private static IQueryable<ItemMovement> ExcludeMovement(
            IQueryable<ItemMovement> query,
            InventoryMovementReference? movementReference)
    {
        if (movementReference is null)
        {
            return query;
        }

        var movementTypes = movementReference.MovementTypes
            .Distinct()
            .ToArray();

        return query.Where(movement =>
            !movementTypes.Contains(movement.MovementType) ||
            movement.ReferenceId != movementReference.ReferenceId ||
            movement.ReferenceNumber != movementReference.ReferenceNumber);
    }

    private sealed record StockEvent(
        DateOnly Date,
        int Priority,
        int Id,
        decimal QuantityIn,
        decimal QuantityOut,
        bool IsProposed);
}
