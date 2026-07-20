using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Stores;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Stores;

public sealed class StoreService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyService currentCompanyService)
    : IStoreService, IScopedService
{
    public async Task<Result<PagedResponse<StoreResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result<PagedResponse<StoreResponse>>.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var query = dbContext.Stores
            .AsNoTracking()
            .Where(store => store.CompanyId == companyId)
            .OrderBy(store => store.Name)
            .ThenBy(store => store.Id);

        return await paginationService.PaginateAsync<Store, StoreResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result<IReadOnlyList<SelectResponse>>.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var response = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.CompanyId == companyId && store.IsActive)
            .OrderBy(store => store.Name)
            .ThenBy(store => store.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<StoreResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StoreResponse>.Failure(InvalidId());
        }

        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result<StoreResponse>.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var response = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.Id == id && store.CompanyId == companyId)
            .ProjectToType<StoreResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<StoreResponse>.Failure(NotFound(id))
            : Result<StoreResponse>.Success(response);
    }

    public async Task<Result<StoreResponse>> AddAsync(
        StoreRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result<StoreResponse>.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var store = request.Adapt<Store>();
        store.CompanyId = companyId;
        var codeExists = await dbContext.Stores.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Code == store.Code,
            cancellationToken);

        if (codeExists)
        {
            return Result<StoreResponse>.Failure(CodeExists(store.Code));
        }

        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<StoreResponse>.Success(store.Adapt<StoreResponse>());
    }

    public async Task<Result<StoreResponse>> UpdateAsync(
        int id,
        StoreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StoreResponse>.Failure(InvalidId());
        }

        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result<StoreResponse>.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var store = await dbContext.Stores.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (store is null)
        {
            return Result<StoreResponse>.Failure(NotFound(id));
        }

        var normalizedStore = request.Adapt<Store>();
        var codeExists = await dbContext.Stores.AnyAsync(
            entity =>
                entity.CompanyId == companyId &&
                entity.Code == normalizedStore.Code &&
                entity.Id != id,
            cancellationToken);

        if (codeExists)
        {
            return Result<StoreResponse>.Failure(CodeExists(normalizedStore.Code));
        }

        request.Adapt(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<StoreResponse>.Success(store.Adapt<StoreResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var companyResult = currentCompanyService.GetCompanyId();
        if (companyResult.IsFailure)
        {
            return Result.Failure(companyResult.Error);
        }

        var companyId = companyResult.Value;
        var store = await dbContext.Stores.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (store is null)
        {
            return Result.Failure(NotFound(id));
        }

        store.IsActive = false;
        dbContext.Stores.Remove(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Error InvalidId() =>
        Error.Validation("Stores.InvalidId", "Store ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound("Stores.NotFound", $"Store with ID {id} was not found.");

    private static Error CodeExists(string code) =>
        Error.Conflict("Stores.CodeExists", $"Store code '{code}' already exists.");
}
