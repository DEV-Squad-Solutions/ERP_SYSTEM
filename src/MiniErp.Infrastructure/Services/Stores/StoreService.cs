using Mapster;
using static MiniErp.Application.Features.Stores.StoreErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Stores;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Stores;

public sealed class StoreService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IStoreService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StoreResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StoreFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new StoreFilterRequest();
        var query = dbContext.Stores
            .AsNoTracking()
            .Where(store => store.CompanyId == companyId)
            .Where(store =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                store.Code.Contains(filters.Search.Trim()) ||
                store.Name.Contains(filters.Search.Trim()) ||
                (store.Address != null &&
                 store.Address.Contains(filters.Search.Trim())))
            .Where(store =>
                string.IsNullOrWhiteSpace(filters.Code) ||
                store.Code.Contains(filters.Code.Trim()))
            .Where(store =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                store.Name.Contains(filters.Name.Trim()))
            .Where(store =>
                !filters.BusinessPartnerId.HasValue ||
                store.BusinessPartnerId == filters.BusinessPartnerId.Value)
            .Where(store =>
                !filters.IsContainerStore.HasValue ||
                store.IsContainerStore == filters.IsContainerStore.Value)
            .Where(store =>
                !filters.IsActive.HasValue ||
                store.IsActive == filters.IsActive.Value)
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
        var response = await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CompanyId == companyId &&
                store.IsActive &&
                !store.IsContainerStore)
            .OrderBy(store => store.Name)
            .ThenBy(store => store.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetContainerSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CompanyId == companyId &&
                store.IsActive &&
                store.IsContainerStore &&
                store.BusinessPartner != null &&
                store.BusinessPartner.IsActive)
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

        var businessPartnerError = await ValidateBusinessPartnerAsync(
            store,
            null,
            cancellationToken);
        if (businessPartnerError is not null)
        {
            return Result<StoreResponse>.Failure(businessPartnerError);
        }

        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(store.Id, cancellationToken);
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

        var store = await dbContext.Stores.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (store is null)
        {
            return Result<StoreResponse>.Failure(NotFound(id));
        }

        var normalizedStore = request.Adapt<Store>();
        var typeChanged =
            store.IsContainerStore != normalizedStore.IsContainerStore;
        var businessPartnerChanged =
            store.BusinessPartnerId != normalizedStore.BusinessPartnerId;

        if ((typeChanged || businessPartnerChanged) &&
            await HasHistoricalContainerAssignmentsAsync(id, cancellationToken))
        {
            return Result<StoreResponse>.Failure(HasContainerAssignments());
        }

        if ((typeChanged &&
             await HasHistoricalDependenciesAsync(id, cancellationToken)) ||
            (!typeChanged &&
             businessPartnerChanged &&
             await HasHistoricalContainerStoreDependenciesAsync(
                 id,
                 cancellationToken)))
        {
            return Result<StoreResponse>.Failure(
                HistoricalIdentityChangeNotAllowed());
        }

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

        var businessPartnerError = await ValidateBusinessPartnerAsync(
            normalizedStore,
            id,
            cancellationToken);
        if (businessPartnerError is not null)
        {
            return Result<StoreResponse>.Failure(businessPartnerError);
        }

        request.Adapt(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(store.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var store = await dbContext.Stores.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);

        if (store is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (await HasHistoricalContainerAssignmentsAsync(id, cancellationToken))
        {
            return Result.Failure(HasContainerAssignments());
        }

        if (await HasHistoricalDependenciesAsync(id, cancellationToken))
        {
            return Result.Failure(HasDependencies());
        }

        store.IsActive = false;
        dbContext.Stores.Remove(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<bool> HasHistoricalContainerAssignmentsAsync(
        int storeId,
        CancellationToken cancellationToken) =>
        dbContext.StoreContainers
            .IgnoreQueryFilters()
            .AnyAsync(
                assignment =>
                    assignment.CompanyId == companyId &&
                    assignment.StoreId == storeId,
                cancellationToken);

    private async Task<bool> HasHistoricalDependenciesAsync(
        int storeId,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    (invoice.StoreId == storeId ||
                     invoice.ContainerStoreId == storeId),
                cancellationToken) ||
        await dbContext.StockOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.StoreId == storeId,
                cancellationToken) ||
        await dbContext.StockAdjustments
            .IgnoreQueryFilters()
            .AnyAsync(
                adjustment =>
                    adjustment.CompanyId == companyId &&
                    adjustment.StoreId == storeId,
                cancellationToken) ||
        await dbContext.InventoryCounts
            .IgnoreQueryFilters()
            .AnyAsync(
                count =>
                    count.CompanyId == companyId &&
                    count.StoreId == storeId,
                cancellationToken) ||
        await dbContext.ItemMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.StoreId == storeId,
                cancellationToken) ||
        await dbContext.ContainerMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.ContainerStoreId == storeId,
                cancellationToken);

    private async Task<bool> HasHistoricalContainerStoreDependenciesAsync(
        int storeId,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.ContainerStoreId == storeId,
                cancellationToken) ||
        await dbContext.ContainerMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.ContainerStoreId == storeId,
                cancellationToken);

    private async Task<Error?> ValidateBusinessPartnerAsync(
        Store store,
        int? excludedStoreId,
        CancellationToken cancellationToken)
    {
        if (!store.IsContainerStore)
        {
            return store.BusinessPartnerId is null
                ? null
                : ProductStoreBusinessPartner();
        }

        if (store.BusinessPartnerId is null or <= 0)
        {
            return InvalidBusinessPartnerId();
        }

        var businessPartner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.Id == store.BusinessPartnerId.Value)
            .Select(partner => new { partner.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (businessPartner is null)
        {
            return BusinessPartnerNotFound(store.BusinessPartnerId.Value);
        }

        if (!businessPartner.IsActive)
        {
            return BusinessPartnerInactive();
        }

        if (!store.IsActive)
        {
            return null;
        }

        var activeContainerStoreExists = await dbContext.Stores
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.BusinessPartnerId == store.BusinessPartnerId &&
                    candidate.IsContainerStore &&
                    candidate.IsActive &&
                    (!excludedStoreId.HasValue ||
                     candidate.Id != excludedStoreId.Value),
                cancellationToken);

        return activeContainerStoreExists
            ? ActiveContainerStoreExists(store.BusinessPartnerId.Value)
            : null;
    }

}
