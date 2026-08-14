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
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService)
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

        var asOfDate = filters.AsOfDate ?? DateOnly.MaxValue;

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
                BuildEmptyResponse(store, asOfDate, baseCurrency, pagination));
        }

        var itemIds = items.Select(item => item.Id).ToArray();
        var balances = await inventoryStockService.GetBalancesAsync(
            store.Id,
            itemIds,
            asOfDate,
            cancellationToken: cancellationToken);
        var costSnapshots = await inventoryCostingService.GetSnapshotsAsync(
            store.Id,
            itemIds,
            asOfDate,
            cancellationToken);

        var reportItems = items
            .Select(item =>
            {
                var balance = balances.GetValueOrDefault(item.Id);
                var cost = costSnapshots[item.Id];
                return new ReportRow(
                    item,
                    balance,
                    cost.AverageCost,
                    cost.InventoryValue);
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
                StoreId: store.Id,
                StoreCode: store.Code,
                StoreName: store.Name,
                AsOfDate: asOfDate == DateOnly.MaxValue
                    ? DateOnly.FromDateTime(DateTime.UtcNow)
                    : asOfDate,
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
        StoreProjection store,
        DateOnly asOfDate,
        CurrencyCode baseCurrency,
        PaginationRequest pagination) =>
        new(
            StoreId: store.Id,
            StoreCode: store.Code,
            StoreName: store.Name,
            AsOfDate: asOfDate == DateOnly.MaxValue
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : asOfDate,
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
}
