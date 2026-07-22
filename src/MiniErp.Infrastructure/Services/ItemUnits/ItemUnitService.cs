using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ItemUnits;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ItemUnits;

public sealed class ItemUnitService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IItemUnitService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ItemUnitResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ItemUnits
            .AsNoTracking()
            .Where(itemUnit => itemUnit.CompanyId == companyId)
            .OrderBy(itemUnit => itemUnit.Name)
            .ThenBy(itemUnit => itemUnit.Id);

        return await paginationService.PaginateAsync<ItemUnit, ItemUnitResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.ItemUnits
            .AsNoTracking()
            .Where(itemUnit =>
                itemUnit.CompanyId == companyId &&
                itemUnit.IsActive)
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
            .Where(entity => entity.Id == id && entity.CompanyId == companyId)
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
        var itemUnit = request.Adapt<ItemUnit>();
        itemUnit.CompanyId = companyId;
        var nameExists = await dbContext.ItemUnits.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Name == itemUnit.Name,
            cancellationToken);

        if (nameExists)
        {
            return Result<ItemUnitResponse>.Failure(
                Error.Conflict(
                    "ItemUnits.NameExists",
                    $"وحدة الصنف '{itemUnit.Name}' موجودة بالفعل."));
        }

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
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result<ItemUnitResponse>.Failure(NotFound(id));
        }

        var normalizedItemUnit = request.Adapt<ItemUnit>();
        var nameExists = await dbContext.ItemUnits.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Name == normalizedItemUnit.Name &&
                entity.Id != id,
            cancellationToken);

        if (nameExists)
        {
            return Result<ItemUnitResponse>.Failure(
                Error.Conflict(
                    "ItemUnits.NameExists",
                    $"وحدة الصنف '{normalizedItemUnit.Name}' موجودة بالفعل."));
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
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (itemUnit is null)
        {
            return Result.Failure(NotFound(id));
        }

        var isInUse = await dbContext.Items
            .IgnoreQueryFilters()
            .AnyAsync(
                item =>
                    item.CompanyId == companyId &&
                    item.ItemUnitId == id,
                cancellationToken);

        if (isInUse)
        {
            return Result.Failure(
                Error.Conflict(
                    "ItemUnits.InUse",
                    "لا يمكن حذف وحدة الصنف لأنها مستخدمة في صنف حالي أو تاريخي واحد على الأقل."));
        }

        itemUnit.IsActive = false;
        dbContext.ItemUnits.Remove(itemUnit);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Error InvalidId() =>
        Error.Validation("ItemUnits.InvalidId", "يجب أن يكون رقم وحدة الصنف أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "ItemUnits.NotFound",
            $"لم يتم العثور على وحدة الصنف رقم {id}.");
}
