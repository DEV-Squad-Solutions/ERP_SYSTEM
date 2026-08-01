using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.InventoryCostReports.InventoryCostReportErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.InventoryCostReports;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed class InventoryCostReportService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IInventoryCostReportService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InventoryCostReportResponse>> GetAsync(
        PaginationRequest pagination,
        InventoryCostReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<InventoryCostReportResponse>.Failure(
                paginationError);
        }

        if (!filters.StoreId.HasValue || filters.StoreId.Value <= 0)
        {
            return Result<InventoryCostReportResponse>.Failure(StoreRequired());
        }

        if (!filters.ItemId.HasValue || filters.ItemId.Value <= 0)
        {
            return Result<InventoryCostReportResponse>.Failure(ItemRequired());
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.StoreId.Value)
            .Select(entity => new
            {
                entity.Id,
                entity.Code,
                entity.Name,
                entity.IsContainerStore
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<InventoryCostReportResponse>.Failure(StoreNotFound());
        }

        if (store.IsContainerStore)
        {
            return Result<InventoryCostReportResponse>.Failure(ProductStoreRequired());
        }

        var item = await dbContext.Items
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.ItemId.Value)
            .Select(entity => new
            {
                entity.Id,
                entity.Code,
                entity.Name,
                ItemUnitName = entity.ItemUnit.Name
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return Result<InventoryCostReportResponse>.Failure(ItemNotFound());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        var timeline = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.StoreId == store.Id &&
                movement.ItemId == item.Id)
            .OrderBy(movement => movement.MovementDate)
            .ThenBy(movement => movement.CreatedOn)
            .ThenBy(movement => movement.Id)
            .Select(movement => new MovementProjection
            {
                Id = movement.Id,
                MovementDate = movement.MovementDate,
                CreatedOn = movement.CreatedOn,
                MovementType = movement.MovementType,
                ReferenceId = movement.ReferenceId,
                ReferenceNumber = movement.ReferenceNumber,
                Description = movement.Description,
                QuantityIn = movement.QuantityIn,
                QuantityOut = movement.QuantityOut,
                CostStatus = movement.CostStatus,
                PendingCostQuantity = movement.PendingCostQuantity,
                UnitCost = movement.UnitCost,
                TotalCost = movement.TotalCost,
                QuantityAfter = movement.QuantityAfter,
                AverageCostAfter = movement.AverageCostAfter,
                InventoryValueAfter = movement.InventoryValueAfter
            })
            .ToListAsync(cancellationToken);

        var reportMovements = timeline
            .Where(movement =>
                (!filters.FromDate.HasValue ||
                 movement.MovementDate >= filters.FromDate.Value) &&
                (!filters.ToDate.HasValue ||
                 movement.MovementDate <= filters.ToDate.Value) &&
                (!filters.MovementType.HasValue ||
                 movement.MovementType == filters.MovementType.Value) &&
                (!filters.CostStatus.HasValue ||
                 movement.CostStatus == filters.CostStatus.Value) &&
                MatchesSearch(movement, filters.Search))
            .ToArray();

        var totalCount = reportMovements.Length;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var pageMovements = offset >= totalCount
            ? []
            : reportMovements
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToArray();

        var pageMovementIds = pageMovements
            .Select(movement => movement.Id)
            .ToArray();
        var rawAllocations = pageMovementIds.Length == 0
            ? []
            : await dbContext.InventoryCostAllocations
                .AsNoTracking()
                .Where(allocation =>
                    allocation.CompanyId == companyId &&
                    allocation.StoreId == store.Id &&
                    allocation.ItemId == item.Id &&
                    (pageMovementIds.Contains(allocation.OutboundMovementId) ||
                     pageMovementIds.Contains(allocation.InboundMovementId)))
                .Select(allocation => new AllocationProjection
                {
                    Id = allocation.Id,
                    OutboundMovementId = allocation.OutboundMovementId,
                    InboundMovementId = allocation.InboundMovementId,
                    Quantity = allocation.Quantity,
                    UnitCost = allocation.UnitCost,
                    TotalCost = allocation.TotalCost
                })
                .ToListAsync(cancellationToken);

        var timelineById = timeline.ToDictionary(movement => movement.Id);
        var allocationByMovementId = rawAllocations
            .SelectMany(allocation => new[]
            {
                new
                {
                    MovementId = allocation.OutboundMovementId,
                    IsInboundAllocation = false,
                    Allocation = allocation
                },
                new
                {
                    MovementId = allocation.InboundMovementId,
                    IsInboundAllocation = true,
                    Allocation = allocation
                }
            })
            .Where(entry => pageMovementIds.Contains(entry.MovementId))
            .GroupBy(entry => entry.MovementId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(entry => entry.Allocation.Id)
                    .Select(entry =>
                    {
                        var relatedId = entry.IsInboundAllocation
                            ? entry.Allocation.OutboundMovementId
                            : entry.Allocation.InboundMovementId;
                        var related = timelineById[relatedId];
                        return new InventoryCostAllocationReportResponse(
                            entry.Allocation.Id,
                            entry.IsInboundAllocation,
                            related.Id,
                            related.MovementDate,
                            related.MovementType,
                            related.ReferenceNumber,
                            entry.Allocation.Quantity,
                            entry.Allocation.UnitCost,
                            entry.Allocation.TotalCost);
                    })
                    .ToArray());

        var items = pageMovements
            .Select(movement => new InventoryCostReportItemResponse(
                movement.Id,
                movement.MovementDate,
                movement.CreatedOn,
                movement.MovementType,
                movement.ReferenceId,
                movement.ReferenceNumber,
                movement.Description,
                movement.QuantityIn,
                movement.QuantityOut,
                movement.CostStatus,
                movement.PendingCostQuantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.QuantityAfter,
                movement.AverageCostAfter,
                movement.InventoryValueAfter,
                allocationByMovementId.GetValueOrDefault(
                    movement.Id,
                    [])))
            .ToArray();

        var openingMovement = timeline
            .Where(movement =>
                filters.FromDate.HasValue &&
                movement.MovementDate < filters.FromDate.Value)
            .LastOrDefault();
        var closingMovement = timeline
            .Where(movement =>
                !filters.ToDate.HasValue ||
                movement.MovementDate <= filters.ToDate.Value)
            .LastOrDefault();
        var periodMovements = timeline
            .Where(movement =>
                (!filters.FromDate.HasValue ||
                 movement.MovementDate >= filters.FromDate.Value) &&
                (!filters.ToDate.HasValue ||
                 movement.MovementDate <= filters.ToDate.Value))
            .ToArray();
        var asOfMovements = timeline
            .Where(movement =>
                !filters.ToDate.HasValue ||
                movement.MovementDate <= filters.ToDate.Value)
            .ToArray();

        var currentBalance = await dbContext.ItemStoreBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.StoreId == store.Id &&
                balance.ItemId == item.Id)
            .Select(balance => new
            {
                balance.Quantity,
                balance.AverageCost,
                balance.InventoryValue
            })
            .SingleOrDefaultAsync(cancellationToken);

        var summary = new InventoryCostReportSummaryResponse(
            openingMovement?.QuantityAfter ?? 0m,
            openingMovement?.AverageCostAfter ?? 0m,
            openingMovement?.InventoryValueAfter ?? 0m,
            periodMovements.Sum(movement => movement.QuantityIn),
            periodMovements.Sum(movement => movement.QuantityOut),
            periodMovements
                .Where(movement => movement.QuantityIn > 0m)
                .Sum(movement => movement.TotalCost),
            periodMovements
                .Where(movement => movement.QuantityOut > 0m)
                .Sum(movement => movement.TotalCost),
            closingMovement?.QuantityAfter ?? 0m,
            closingMovement?.AverageCostAfter ?? 0m,
            closingMovement?.InventoryValueAfter ?? 0m,
            currentBalance?.Quantity ?? 0m,
            currentBalance?.AverageCost ?? 0m,
            currentBalance?.InventoryValue ?? 0m,
            asOfMovements
                .Where(movement =>
                    movement.QuantityOut > 0m &&
                    movement.PendingCostQuantity > 0m)
                .Sum(movement => movement.PendingCostQuantity),
            asOfMovements.Count(movement =>
                movement.CostStatus is
                    InventoryCostStatus.Pending or
                    InventoryCostStatus.PartiallyCosted),
            periodMovements.Count(movement =>
                movement.CostStatus == InventoryCostStatus.Revalued));

        return Result<InventoryCostReportResponse>.Success(
            new InventoryCostReportResponse(
                store.Id,
                store.Code,
                store.Name,
                item.Id,
                item.Code,
                item.Name,
                item.ItemUnitName,
                baseCurrency,
                filters.FromDate,
                filters.ToDate,
                items,
                pagination.PageNumber,
                pagination.PageSize,
                totalCount,
                (int)Math.Ceiling(
                    totalCount / (double)pagination.PageSize),
                summary));
    }

    private static bool MatchesSearch(
        MovementProjection movement,
        string? search)
    {
        var value = search?.Trim();
        return string.IsNullOrEmpty(value) ||
            movement.ReferenceNumber.Contains(value) ||
            (movement.Description?.Contains(value) ?? false);
    }

    private static Error? ValidatePagination(PaginationRequest pagination) =>
        pagination.PageNumber <= 0 ||
        pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize
            ? PaginationErrors.Invalid()
            : null;

    private sealed class MovementProjection
    {
        public int Id { get; init; }
        public DateOnly MovementDate { get; init; }
        public DateTime CreatedOn { get; init; }
        public ItemMovementType MovementType { get; init; }
        public int ReferenceId { get; init; }
        public string ReferenceNumber { get; init; } = string.Empty;
        public string? Description { get; init; }
        public decimal QuantityIn { get; init; }
        public decimal QuantityOut { get; init; }
        public InventoryCostStatus CostStatus { get; init; }
        public decimal PendingCostQuantity { get; init; }
        public decimal? UnitCost { get; init; }
        public decimal TotalCost { get; init; }
        public decimal QuantityAfter { get; init; }
        public decimal AverageCostAfter { get; init; }
        public decimal InventoryValueAfter { get; init; }
    }

    private sealed class AllocationProjection
    {
        public long Id { get; init; }
        public int OutboundMovementId { get; init; }
        public int InboundMovementId { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public decimal TotalCost { get; init; }
    }
}
