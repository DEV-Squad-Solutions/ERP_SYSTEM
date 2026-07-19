using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Items;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Items;

public sealed class ItemService(ApplicationDbContext dbContext)
    : IItemService, IScopedService
{
    public async Task<Result<IReadOnlyList<ItemResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Items
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ProjectToType<ItemResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ItemResponse>>.Success(response);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Items
            .AsNoTracking()
            .Where(item => item.IsActive)
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

        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity => entity.Id == request.ItemUnitId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemResponse>.Failure(
                Error.NotFound(
                    "ItemUnits.NotFound",
                    $"Item unit with ID {request.ItemUnitId} was not found."));
        }

        var item = request.Adapt<Item>();

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

        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity => entity.Id == request.ItemUnitId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemResponse>.Failure(
                Error.NotFound(
                    "ItemUnits.NotFound",
                    $"Item unit with ID {request.ItemUnitId} was not found."));
        }

        request.Adapt(item);

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
}
