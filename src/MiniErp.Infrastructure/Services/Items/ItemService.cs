using Mapster;
using static MiniErp.Application.Features.Items.ItemErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Items;
using MiniErp.Application.Features.ItemUnits;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Items;

public sealed class ItemService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IItemService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ItemResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ItemFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new ItemFilterRequest();
        var query = dbContext.Items
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .Where(item =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                item.Code.Contains(filters.Search.Trim()) ||
                item.Name.Contains(filters.Search.Trim()) ||
                (item.Description != null &&
                 item.Description.Contains(filters.Search.Trim())))
            .Where(item =>
                string.IsNullOrWhiteSpace(filters.Code) ||
                item.Code.Contains(filters.Code.Trim()))
            .Where(item =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                item.Name.Contains(filters.Name.Trim()))
            .Where(item =>
                !filters.ItemUnitId.HasValue ||
                item.ItemUnitId == filters.ItemUnitId.Value)
            .Where(item =>
                !filters.IsActive.HasValue ||
                item.IsActive == filters.IsActive.Value)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id);

        return await paginationService.PaginateAsync<Item, ItemResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                item.IsActive &&
                item.ItemUnit.IsActive)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<ItemResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.Id == id && entity.CompanyId == companyId)
            .ProjectToType<ItemResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ItemResponse>.Failure(NotFound(id))
            : Result<ItemResponse>.Success(response);
    }

    public async Task<Result<ItemResponse>> AddAsync(
        ItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = request.Adapt<Item>();
        item.CompanyId = companyId;
        var codeExists = await dbContext.Items.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Code == item.Code,
            cancellationToken);

        if (codeExists)
        {
            return Result<ItemResponse>.Failure(CodeExists(item.Code));
        }

        var itemUnitResult = await GetActiveItemUnitAsync(
            request.ItemUnitId,
            companyId,
            cancellationToken);
        if (itemUnitResult.IsFailure)
        {
            return Result<ItemResponse>.Failure(itemUnitResult.Error);
        }

        item.ItemUnit = itemUnitResult.Value;
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ItemResponse>.Success(item.Adapt<ItemResponse>());
    }

    public async Task<Result<ItemResponse>> UpdateAsync(
        int id,
        ItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemResponse>.Failure(InvalidId());
        }

        var item = await dbContext.Items.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (item is null)
        {
            return Result<ItemResponse>.Failure(NotFound(id));
        }

        var normalizedItem = request.Adapt<Item>();
        var codeExists = await dbContext.Items.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Code == normalizedItem.Code &&
                entity.Id != id,
            cancellationToken);

        if (codeExists)
        {
            return Result<ItemResponse>.Failure(CodeExists(normalizedItem.Code));
        }

        var itemUnitResult = await GetActiveItemUnitAsync(
            request.ItemUnitId,
            companyId,
            cancellationToken);
        if (itemUnitResult.IsFailure)
        {
            return Result<ItemResponse>.Failure(itemUnitResult.Error);
        }

        request.Adapt(item);
        item.ItemUnit = itemUnitResult.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ItemResponse>.Success(item.Adapt<ItemResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var item = await dbContext.Items.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (item is null)
        {
            return Result.Failure(NotFound(id));
        }

        var isInUse = await dbContext.InvoiceLines
            .IgnoreQueryFilters()
            .AnyAsync(
                line =>
                    line.CompanyId == companyId &&
                    line.ItemId == id,
                cancellationToken) ||
            await dbContext.StockOpeningBalanceLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.ItemId == id,
                    cancellationToken) ||
            await dbContext.StockAdjustmentLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.ItemId == id,
                    cancellationToken) ||
            await dbContext.InventoryCountLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.ItemId == id,
                    cancellationToken) ||
            await dbContext.ItemMovements
                .IgnoreQueryFilters()
                .AnyAsync(
                    movement =>
                        movement.CompanyId == companyId &&
                        movement.ItemId == id,
                    cancellationToken);
        if (isInUse)
        {
            return Result.Failure(InUse());
        }

        item.IsActive = false;
        dbContext.Items.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<ItemUnit>> GetActiveItemUnitAsync(
        int itemUnitId,
        int companyId,
        CancellationToken cancellationToken)
    {
        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity =>
                entity.Id == itemUnitId &&
                entity.CompanyId == companyId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemUnit>.Failure(ItemUnitErrors.NotFound(itemUnitId));
        }

        return !itemUnit.IsActive
            ? Result<ItemUnit>.Failure(ItemUnitErrors.Inactive(itemUnitId))
            : Result<ItemUnit>.Success(itemUnit);
    }
}
