using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Items;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Items;

public sealed class ItemService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService)
    : IItemService, IScopedService
{
    public async Task<Result<PagedResponse<ItemResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Items
            .AsNoTracking()
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
            .Where(item => item.IsActive && item.ItemUnit.IsActive)
            .OrderBy(item => item.Name)
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
            .Where(entity => entity.Id == id)
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
        var code = request.Code.Trim();
        var codeExists = await dbContext.Items.AnyAsync(
            entity => entity.Code == code,
            cancellationToken);

        if (codeExists)
        {
            return Result<ItemResponse>.Failure(
                Error.Conflict("Items.CodeExists", $"Item code '{code}' already exists."));
        }

        var itemUnitResult = await GetActiveItemUnitAsync(
            request.ItemUnitId,
            cancellationToken);
        if (itemUnitResult.IsFailure)
        {
            return Result<ItemResponse>.Failure(itemUnitResult.Error);
        }

        var item = request.Adapt<Item>();
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
            entity => entity.Id == id,
            cancellationToken);

        if (item is null)
        {
            return Result<ItemResponse>.Failure(NotFound(id));
        }

        var code = request.Code.Trim();
        var codeExists = await dbContext.Items.AnyAsync(
            entity => entity.Code == code && entity.Id != id,
            cancellationToken);

        if (codeExists)
        {
            return Result<ItemResponse>.Failure(
                Error.Conflict("Items.CodeExists", $"Item code '{code}' already exists."));
        }

        var itemUnitResult = await GetActiveItemUnitAsync(
            request.ItemUnitId,
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
            entity => entity.Id == id,
            cancellationToken);

        if (item is null)
        {
            return Result.Failure(NotFound(id));
        }

        item.IsActive = false;
        dbContext.Items.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Error InvalidId() =>
        Error.Validation("Items.InvalidId", "Item ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound("Items.NotFound", $"Item with ID {id} was not found.");

    private async Task<Result<ItemUnit>> GetActiveItemUnitAsync(
        int itemUnitId,
        CancellationToken cancellationToken)
    {
        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity => entity.Id == itemUnitId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemUnit>.Failure(
                Error.NotFound(
                    "ItemUnits.NotFound",
                    $"Item unit with ID {itemUnitId} was not found."));
        }

        return !itemUnit.IsActive
            ? Result<ItemUnit>.Failure(
                Error.Conflict(
                    "ItemUnits.Inactive",
                    $"Item unit with ID {itemUnitId} is inactive."))
            : Result<ItemUnit>.Success(itemUnit);
    }
}
