using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ItemUnits;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ItemUnits;

public sealed class ItemUnitService(ApplicationDbContext dbContext)
    : IItemUnitService, IScopedService
{
    public async Task<Result<IReadOnlyList<ItemUnitResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.ItemUnits
            .AsNoTracking()
            .OrderBy(itemUnit => itemUnit.Name)
            .ProjectToType<ItemUnitResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ItemUnitResponse>>.Success(response);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.ItemUnits
            .AsNoTracking()
            .Where(itemUnit => itemUnit.IsActive)
            .OrderBy(itemUnit => itemUnit.Name)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<ItemUnitResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemUnitResponse>.Failure(InvalidId());
        }

        var response = await dbContext.ItemUnits
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .ProjectToType<ItemUnitResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ItemUnitResponse>.Failure(NotFound(id))
            : Result<ItemUnitResponse>.Success(response);
    }

    public async Task<Result<ItemUnitResponse>> AddAsync(
        ItemUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var nameExists = await dbContext.ItemUnits.AnyAsync(
            entity => entity.Name == name,
            cancellationToken);

        if (nameExists)
        {
            return Result<ItemUnitResponse>.Failure(
                Error.Conflict(
                    "ItemUnits.NameExists",
                    $"Item unit '{name}' already exists."));
        }

        var itemUnit = request.Adapt<ItemUnit>();

        dbContext.ItemUnits.Add(itemUnit);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ItemUnitResponse>.Success(itemUnit.Adapt<ItemUnitResponse>());
    }

    public async Task<Result<ItemUnitResponse>> UpdateAsync(
        int id,
        ItemUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemUnitResponse>.Failure(InvalidId());
        }

        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemUnitResponse>.Failure(NotFound(id));
        }

        var name = request.Name.Trim();
        var nameExists = await dbContext.ItemUnits.AnyAsync(
            entity => entity.Name == name && entity.Id != id,
            cancellationToken);

        if (nameExists)
        {
            return Result<ItemUnitResponse>.Failure(
                Error.Conflict(
                    "ItemUnits.NameExists",
                    $"Item unit '{name}' already exists."));
        }

        request.Adapt(itemUnit);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ItemUnitResponse>.Success(itemUnit.Adapt<ItemUnitResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var itemUnit = await dbContext.ItemUnits.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result.Failure(NotFound(id));
        }

        var isInUse = await dbContext.Items.AnyAsync(
            item => item.ItemUnitId == id,
            cancellationToken);

        if (isInUse)
        {
            return Result.Failure(
                Error.Conflict(
                    "ItemUnits.InUse",
                    "The item unit cannot be deleted because it is used by one or more items."));
        }

        itemUnit.IsActive = false;
        dbContext.ItemUnits.Remove(itemUnit);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Error InvalidId() =>
        Error.Validation("ItemUnits.InvalidId", "Item unit ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "ItemUnits.NotFound",
            $"Item unit with ID {id} was not found.");
}
