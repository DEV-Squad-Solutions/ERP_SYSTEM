using Mapster;
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
        CancellationToken cancellationToken = default)
    {
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

        store.IsActive = false;
        dbContext.Stores.Remove(store);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Error InvalidId() =>
        Error.Validation("Stores.InvalidId", "يجب أن يكون رقم المخزن أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound("Stores.NotFound", $"لم يتم العثور على المخزن رقم {id}.");

    private static Error CodeExists(string code) =>
        Error.Conflict("Stores.CodeExists", $"كود المخزن '{code}' مستخدم بالفعل.");

    private async Task<Error?> ValidateBusinessPartnerAsync(
        Store store,
        CancellationToken cancellationToken)
    {
        if (!store.IsContainerStore)
        {
            return store.BusinessPartnerId is null
                ? null
                : Error.Validation(
                    "Stores.ProductStoreBusinessPartner",
                    "يجب عدم تحديد عميل أو مورد لمخزن المنتجات.");
        }

        if (store.BusinessPartnerId is null or <= 0)
        {
            return Error.Validation(
                "Stores.InvalidBusinessPartnerId",
                "يجب تحديد عميل أو مورد صحيح للمخزن المخصص للعبوات.");
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
            return Error.NotFound(
                "Stores.BusinessPartnerNotFound",
                $"لم يتم العثور على العميل أو المورد رقم {store.BusinessPartnerId.Value}.");
        }

        return businessPartner.IsActive
            ? null
            : Error.Conflict(
                "Stores.BusinessPartnerInactive",
                "يجب ربط مخزن العبوات بعميل أو مورد نشط.");
    }
}
