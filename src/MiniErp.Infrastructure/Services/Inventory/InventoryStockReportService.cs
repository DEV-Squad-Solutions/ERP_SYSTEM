using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.InventoryStockReports.InventoryStockReportErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.InventoryStockReports;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed class InventoryStockReportService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver)
    : IInventoryStockReportService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InventoryStockReportResponse>> GetAsync(
        PaginationRequest pagination,
        InventoryStockReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<InventoryStockReportResponse>.Failure(paginationError);
        }

        var filterError = ValidateFilters(filters);
        if (filterError is not null)
        {
            return Result<InventoryStockReportResponse>.Failure(filterError);
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            toDate: filters.AsOfDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<InventoryStockReportResponse>.Failure(
                fiscalYear.Errors);
        }

        var fiscalYearScope = fiscalYear.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var asOfDate = filters.AsOfDate ??
            (today < fiscalYearScope.StartDate
                ? fiscalYearScope.StartDate
                : today > fiscalYearScope.EndDate
                    ? fiscalYearScope.EndDate
                    : today);

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.StoreId)
            .Select(entity => new StoreProjection(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.IsContainerStore))
            .SingleOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<InventoryStockReportResponse>.Failure(StoreNotFound());
        }

        if (store.IsContainerStore)
        {
            return Result<InventoryStockReportResponse>.Failure(ProductStoreRequired());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        var itemQuery = dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                item.IsActive &&
                item.ItemUnit.IsActive);

        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            itemQuery = itemQuery.Where(item =>
                item.Code.Contains(search) ||
                item.Name.Contains(search) ||
                (item.Description != null &&
                 item.Description.Contains(search)));
        }

        if (filters.ItemId.HasValue)
        {
            itemQuery = itemQuery.Where(item =>
                item.Id == filters.ItemId.Value);
        }

        if (filters.ItemUnitId.HasValue)
        {
            itemQuery = itemQuery.Where(item =>
                item.ItemUnitId == filters.ItemUnitId.Value);
        }

        var items = await itemQuery
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new ItemProjection(
                item.Id,
                item.Code,
                item.Name,
                item.ItemUnitId,
                item.ItemUnit.Name))
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result<InventoryStockReportResponse>.Success(
                BuildEmptyResponse(
                    fiscalYearScope,
                    store,
                    asOfDate,
                    baseCurrency,
                    pagination));
        }

        var itemIds = items.Select(item => item.Id).ToArray();
        var movementRows = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYearScope.FiscalYearId &&
                movement.StoreId == store.Id &&
                itemIds.Contains(movement.ItemId) &&
                movement.MovementDate <= asOfDate)
            .Select(movement => new
            {
                movement.Id,
                movement.ItemId,
                movement.MovementDate,
                movement.CreatedOn,
                movement.QuantityIn,
                movement.QuantityOut,
                movement.AverageCostAfter
            })
            .ToListAsync(cancellationToken);

        var stockByItem = movementRows
            .GroupBy(movement => movement.ItemId)
            .ToDictionary(
                group => group.Key,
                group => new StockSnapshot(
                    Balance: group.Sum(movement =>
                        movement.QuantityIn - movement.QuantityOut),
                    AverageCost: group
                        .OrderByDescending(movement => movement.MovementDate)
                        .ThenByDescending(movement => movement.CreatedOn)
                        .ThenByDescending(movement => movement.Id)
                        .Select(movement => movement.AverageCostAfter)
                        .First()));

        // Old imported opening documents may not have generated item
        // movements. Include only such documents from the selected year.
        var legacyOpeningRows = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.CompanyId == companyId &&
                line.StockOpeningBalance.FiscalYearId ==
                    fiscalYearScope.FiscalYearId &&
                line.StockOpeningBalance.StoreId == store.Id &&
                line.StockOpeningBalance.DocumentDate <= asOfDate &&
                itemIds.Contains(line.ItemId) &&
                !dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearScope.FiscalYearId &&
                    movement.StoreId == store.Id &&
                    movement.ItemId == line.ItemId &&
                    movement.MovementType == ItemMovementType.OpeningBalance &&
                    movement.ReferenceId == line.StockOpeningBalanceId))
            .Select(line => new
            {
                line.ItemId,
                line.Quantity,
                line.Price
            })
            .ToListAsync(cancellationToken);

        var legacyRows = legacyOpeningRows
            .GroupBy(line => line.ItemId)
            .Select(group => new
            {
                ItemId = group.Key,
                Balance = group.Sum(line => line.Quantity),
                InventoryValue = group.Sum(line => line.Quantity * line.Price)
            });

        foreach (var legacy in legacyRows)
        {
            var current = stockByItem.GetValueOrDefault(legacy.ItemId);
            var currentValue = current is null
                ? 0m
                : current.Balance * current.AverageCost;
            var balance = (current?.Balance ?? 0m) + legacy.Balance;
            stockByItem[legacy.ItemId] = new StockSnapshot(
                Balance: balance,
                AverageCost: balance == 0m
                    ? 0m
                    : (currentValue + legacy.InventoryValue) / balance);
        }

        var reportItems = items
            .Select(item =>
            {
                var stock = stockByItem.GetValueOrDefault(item.Id) ??
                    StockSnapshot.Empty;
                return new ReportRow(
                    item,
                    stock.Balance,
                    stock.AverageCost,
                    stock.Balance * stock.AverageCost);
            })
            .Where(row =>
                !filters.HasStock.HasValue ||
                (filters.HasStock.Value
                    ? row.Balance > 0m
                    : row.Balance <= 0m))
            .ToArray();

        var totalCount = reportItems.Length;
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;

        var pageItems = offset >= totalCount
            ? []
            : reportItems
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(row => new InventoryStockReportItemResponse(
                    ItemId: row.Item.Id,
                    ItemCode: row.Item.Code,
                    ItemName: row.Item.Name,
                    ItemUnitId: row.Item.ItemUnitId,
                    ItemUnitName: row.Item.ItemUnitName,
                    Balance: row.Balance,
                    AverageCost: row.AverageCost,
                    InventoryValue: row.InventoryValue))
                .ToArray();

        var summary = new InventoryStockReportSummaryResponse(
            TotalItemCount: totalCount,
            ItemsWithStockCount: reportItems.Count(row => row.Balance > 0m),
            TotalInventoryValue: reportItems.Sum(row => row.InventoryValue));

        return Result<InventoryStockReportResponse>.Success(
            new InventoryStockReportResponse(
                FiscalYearId: fiscalYearScope.FiscalYearId,
                FiscalYearName: fiscalYearScope.FiscalYearName,
                StoreId: store.Id,
                StoreCode: store.Code,
                StoreName: store.Name,
                AsOfDate: asOfDate,
                BaseCurrency: baseCurrency,
                Items: pageItems,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages,
                Summary: summary));
    }

    private static Error? ValidatePagination(PaginationRequest pagination) =>
        pagination.PageNumber <= 0 ||
        pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize
            ? PaginationErrors.Invalid()
            : null;

    private static Error? ValidateFilters(
        InventoryStockReportFilterRequest filters)
    {
        if (filters.StoreId <= 0)
        {
            return StoreRequired();
        }

        if (filters.Search?.Trim().Length > 200)
        {
            return SearchTooLong();
        }

        if (filters.ItemId is <= 0)
        {
            return ItemInvalid();
        }

        if (filters.ItemUnitId is <= 0)
        {
            return ItemUnitInvalid();
        }

        return null;
    }

    private static InventoryStockReportResponse BuildEmptyResponse(
        FiscalYearQueryScope fiscalYear,
        StoreProjection store,
        DateOnly asOfDate,
        CurrencyCode baseCurrency,
        PaginationRequest pagination) =>
        new(
            FiscalYearId: fiscalYear.FiscalYearId,
            FiscalYearName: fiscalYear.FiscalYearName,
            StoreId: store.Id,
            StoreCode: store.Code,
            StoreName: store.Name,
            AsOfDate: asOfDate,
            BaseCurrency: baseCurrency,
            Items: Array.Empty<InventoryStockReportItemResponse>(),
            PageNumber: pagination.PageNumber,
            PageSize: pagination.PageSize,
            TotalCount: 0,
            TotalPages: 0,
            Summary: new InventoryStockReportSummaryResponse(
                TotalItemCount: 0,
                ItemsWithStockCount: 0,
                TotalInventoryValue: 0m));

    private sealed record StoreProjection(
        int Id,
        string Code,
        string Name,
        bool IsContainerStore);

    private sealed record ItemProjection(
        int Id,
        string Code,
        string Name,
        int ItemUnitId,
        string ItemUnitName);

    private sealed record ReportRow(
        ItemProjection Item,
        decimal Balance,
        decimal AverageCost,
        decimal InventoryValue);

    private sealed record StockSnapshot(
        decimal Balance,
        decimal AverageCost)
    {
        public static StockSnapshot Empty { get; } = new(0m, 0m);
    }
}
