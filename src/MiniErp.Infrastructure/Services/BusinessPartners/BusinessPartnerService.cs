using Mapster;
using static MiniErp.Application.Features.BusinessPartners.BusinessPartnerErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Application.Features.Stores;
using MiniErp.Application.Features.StoreContainers;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.BusinessPartners;

public sealed class BusinessPartnerService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IBusinessPartnerService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<BusinessPartnerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        BusinessPartnerFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new BusinessPartnerFilterRequest();
        var query = ApplyFilters(
            dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner => partner.CompanyId == companyId),
            filters)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id);

        var pageResult = await paginationService.PaginateAsync<
            BusinessPartner,
            BusinessPartnerResponse>(
            query,
            pagination,
            cancellationToken);

        if (pageResult.IsFailure || pageResult.Value.Items.Count == 0)
        {
            return pageResult;
        }

        var partnerIds = pageResult.Value.Items
            .Select(partner => partner.Id)
            .ToArray();

        var containerStores = await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CompanyId == companyId &&
                store.BusinessPartnerId.HasValue &&
                partnerIds.Contains(store.BusinessPartnerId.Value) &&
                store.IsContainerStore &&
                store.IsActive)
            .OrderBy(store => store.Id)
            .ProjectToType<StoreResponse>()
            .ToListAsync(cancellationToken);

        var storeByPartnerId = containerStores
            .Where(store => store.BusinessPartnerId.HasValue)
            .GroupBy(store => store.BusinessPartnerId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var containersByStoreId = await GetAssignedContainersAsync(
            containerStores.Select(store => store.Id).ToArray(),
            cancellationToken);

        var enrichedItems = pageResult.Value.Items
            .Select(partner =>
            {
                storeByPartnerId.TryGetValue(partner.Id, out var containerStore);
                var containers = containerStore is not null &&
                    containersByStoreId.TryGetValue(
                        containerStore.Id,
                        out var assignedContainers)
                    ? assignedContainers
                    : [];

                return partner with
                {
                    ContainerStore = containerStore,
                    Containers = containers
                };
            })
            .ToList();

        return Result<PagedResponse<BusinessPartnerResponse>>.Success(
            pageResult.Value with { Items = enrichedItems });
    }

    private static IQueryable<BusinessPartner> ApplyFilters(
        IQueryable<BusinessPartner> query,
        BusinessPartnerFilterRequest filters)
    {
        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(partner =>
                partner.Code.Contains(search) ||
                partner.Name.Contains(search) ||
                (partner.PhoneNumber != null && partner.PhoneNumber.Contains(search)) ||
                (partner.Email != null && partner.Email.Contains(search)) ||
                (partner.TaxNumber != null && partner.TaxNumber.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filters.Code))
        {
            query = query.Where(partner =>
                partner.Code.Contains(filters.Code.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(filters.Name))
        {
            query = query.Where(partner =>
                partner.Name.Contains(filters.Name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(filters.TaxNumber))
        {
            query = query.Where(partner =>
                partner.TaxNumber != null &&
                partner.TaxNumber.Contains(filters.TaxNumber.Trim()));
        }

        if (filters.Currency.HasValue)
        {
            query = query.Where(partner => partner.Currency == filters.Currency.Value);
        }

        if (filters.IsActive.HasValue)
        {
            query = query.Where(partner => partner.IsActive == filters.IsActive.Value);
        }

        return query;
    }

    public async Task<Result<IReadOnlyList<BusinessPartnerSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .ProjectToType<BusinessPartnerSelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<BusinessPartnerSelectResponse>>.Success(response);
    }

    public async Task<Result<BusinessPartnerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<BusinessPartnerResponse>.Failure(InvalidId());
        }

        var response = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.Id == id &&
                partner.CompanyId == companyId)
            .ProjectToType<BusinessPartnerResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null)
        {
            return Result<BusinessPartnerResponse>.Failure(NotFound(id));
        }

        var containerStore = await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CompanyId == companyId &&
                store.BusinessPartnerId == id &&
                store.IsContainerStore &&
                store.IsActive)
            .OrderBy(store => store.Id)
            .ProjectToType<StoreResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        var containers = await GetAssignedContainersAsync(
            containerStore?.Id ?? 0,
            cancellationToken);

        return Result<BusinessPartnerResponse>.Success(
            response with
            {
                ContainerStore = containerStore,
                Containers = containers
            });
    }

    public async Task<Result<BusinessPartnerContainerStoreResponse>>
        GetContainerStoreAsync(
            int id,
            CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<BusinessPartnerContainerStoreResponse>.Failure(
                InvalidId());
        }

        var partnerExists = await dbContext.BusinessPartners
            .AsNoTracking()
            .AnyAsync(
                partner =>
                    partner.Id == id &&
                    partner.CompanyId == companyId,
                cancellationToken);

        if (!partnerExists)
        {
            return Result<BusinessPartnerContainerStoreResponse>.Failure(
                NotFound(id));
        }

        var containerStore = await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CompanyId == companyId &&
                store.BusinessPartnerId == id &&
                store.IsContainerStore &&
                store.IsActive)
            .OrderBy(store => store.Id)
            .ProjectToType<StoreResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        if (containerStore is null)
        {
            return Result<BusinessPartnerContainerStoreResponse>.Failure(
                ContainerStoreNotFound(id));
        }

        var containers = await GetAssignedContainersAsync(
            containerStore.Id,
            cancellationToken);

        return Result<BusinessPartnerContainerStoreResponse>.Success(
            new BusinessPartnerContainerStoreResponse(
                ContainerStore: containerStore,
                Containers: containers));
    }

    private async Task<IReadOnlyList<StoreContainerWorkspaceContainerResponse>>
        GetAssignedContainersAsync(
            int storeId,
            CancellationToken cancellationToken)
    {
        var containersByStoreId = await GetAssignedContainersAsync(
            [storeId],
            cancellationToken);

        return containersByStoreId.TryGetValue(storeId, out var containers)
            ? containers
            : [];
    }

    private async Task<Dictionary<
        int,
        IReadOnlyList<StoreContainerWorkspaceContainerResponse>>>
        GetAssignedContainersAsync(
            IReadOnlyCollection<int> storeIds,
            CancellationToken cancellationToken)
    {
        var distinctStoreIds = storeIds
            .Where(storeId => storeId > 0)
            .Distinct()
            .ToArray();

        if (distinctStoreIds.Length == 0)
        {
            return [];
        }

        var assignments = await dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                distinctStoreIds.Contains(assignment.StoreId) &&
                assignment.IsActive &&
                assignment.Container.IsActive)
            .OrderBy(assignment => assignment.StoreId)
            .ThenBy(assignment => assignment.Container.Name)
            .ThenBy(assignment => assignment.ContainerId)
            .Select(assignment => new
            {
                assignment.StoreId,
                StoreContainerId = assignment.Id,
                assignment.Container.Id,
                assignment.Container.CompanyId,
                assignment.Container.Code,
                assignment.Container.Name,
                assignment.Container.Description,
                assignment.Container.IsActive
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<
            int,
            IReadOnlyList<StoreContainerWorkspaceContainerResponse>>();

        foreach (var storeId in distinctStoreIds)
        {
            result[storeId] = assignments
                .Where(assignment => assignment.StoreId == storeId)
                .Select(assignment =>
                    new StoreContainerWorkspaceContainerResponse(
                        Id: assignment.Id,
                        CompanyId: assignment.CompanyId,
                        Code: assignment.Code,
                        Name: assignment.Name,
                        Description: assignment.Description,
                        IsActive: assignment.IsActive,
                        IsAssigned: true,
                        StoreContainerId: assignment.StoreContainerId))
                .ToList();
        }

        return result;
    }

    public async Task<Result<BusinessPartnerResponse>> AddAsync(
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var partner = request.Adapt<BusinessPartner>();
        partner.CompanyId = companyId;
        partner.Code = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "BPR",
            companyId: companyId,
            existingIdentifiers: dbContext.BusinessPartners
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.Code),
            cancellationToken);

        var duplicateErrors = await FindDuplicateAsync(
            partner,
            excludedId: null,
            cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateErrors);
        }

        dbContext.BusinessPartners.Add(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BusinessPartnerResponse>.Success(
            partner.Adapt<BusinessPartnerResponse>());
    }

    public async Task<Result<BusinessPartnerResponse>> UpdateAsync(
        int id,
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<BusinessPartnerResponse>.Failure(InvalidId());
        }

        var partner = await dbContext.BusinessPartners.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (partner is null)
        {
            return Result<BusinessPartnerResponse>.Failure(NotFound(id));
        }

        var normalizedPartner = request.Adapt<BusinessPartner>();
        var duplicateErrors = await FindDuplicateAsync(
            normalizedPartner,
            id,
            cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateErrors);
        }

        if (partner.Currency != normalizedPartner.Currency &&
            await HasFinancialRecordsAsync(id, cancellationToken))
        {
            return Result<BusinessPartnerResponse>.Failure(
                CurrencyChangeNotAllowed());
        }

        var code = partner.Code;
        request.Adapt(partner);
        partner.Code = code;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BusinessPartnerResponse>.Success(
            partner.Adapt<BusinessPartnerResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var partner = await dbContext.BusinessPartners.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (partner is null)
        {
            return Result.Failure(NotFound(id));
        }

        var hasContainerStores = await dbContext.Stores
            .IgnoreQueryFilters()
            .AnyAsync(
                store =>
                    store.CompanyId == companyId &&
                    store.BusinessPartnerId == id,
                cancellationToken);
        if (hasContainerStores)
        {
            return Result.Failure(HasContainerStores());
        }

        if (await HasFinancialRecordsAsync(id, cancellationToken))
        {
            return Result.Failure(HasFinancialRecords());
        }

        partner.IsActive = false;
        dbContext.BusinessPartners.Remove(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<IReadOnlyList<Error>> FindDuplicateAsync(
        BusinessPartner partner,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = partner.Name.ToUpperInvariant();
        var normalizedTaxNumber = partner.TaxNumber?.ToUpperInvariant();

        var duplicates = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                (entity.Name.ToUpper() == normalizedName ||
                 (normalizedTaxNumber != null &&
                  entity.TaxNumber != null &&
                  entity.TaxNumber.ToUpper() == normalizedTaxNumber)))
            .Select(entity => new
            {
                entity.Name,
                entity.TaxNumber
            })
            .ToListAsync(cancellationToken);

        var errors = new List<Error>();
        if (duplicates.Any(entity => string.Equals(
                entity.Name,
                partner.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(NameExists(partner.Name));
        }

        if (partner.TaxNumber is not null &&
            duplicates.Any(entity => string.Equals(
                entity.TaxNumber,
                partner.TaxNumber,
                StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(TaxNumberExists());
        }

        return errors;
    }

    private Task<bool> HasFinancialRecordsAsync(
        int businessPartnerId,
        CancellationToken cancellationToken) =>
        dbContext.Companies
            .Where(company => company.Id == companyId)
            .Select(_ =>
                dbContext.Invoices
                    .IgnoreQueryFilters()
                    .Any(invoice =>
                        invoice.CompanyId == companyId &&
                        invoice.BusinessPartnerId == businessPartnerId) ||
                dbContext.PartnerOpeningBalances
                    .IgnoreQueryFilters()
                    .Any(balance =>
                        balance.CompanyId == companyId &&
                        balance.BusinessPartnerId == businessPartnerId) ||
                dbContext.CashVouchers
                    .IgnoreQueryFilters()
                    .Any(voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.BusinessPartnerId == businessPartnerId) ||
                dbContext.BusinessPartnerMovements
                    .IgnoreQueryFilters()
                    .Any(movement =>
                        movement.CompanyId == companyId &&
                        movement.BusinessPartnerId == businessPartnerId) ||
                dbContext.ContainerMovements
                    .IgnoreQueryFilters()
                    .Any(movement =>
                        movement.CompanyId == companyId &&
                        movement.BusinessPartnerId == businessPartnerId) ||
                dbContext.DriverTrips
                    .IgnoreQueryFilters()
                    .Any(trip =>
                        trip.CompanyId == companyId &&
                        trip.BusinessPartnerId == businessPartnerId))
            .FirstAsync(cancellationToken);

}
