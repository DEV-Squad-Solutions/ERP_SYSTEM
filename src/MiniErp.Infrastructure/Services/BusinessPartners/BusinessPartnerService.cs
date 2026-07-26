using Mapster;
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
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner => partner.CompanyId == companyId)
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

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
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

        var containers = await GetContainerWorkspaceAsync(
            containerStore?.Id,
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
                containerStore,
                containers));
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
                        assignment.Id,
                        assignment.CompanyId,
                        assignment.Code,
                        assignment.Name,
                        assignment.Description,
                        assignment.IsActive,
                        true,
                        assignment.StoreContainerId))
                .ToList();
        }

        return result;
    }

    private async Task<IReadOnlyList<StoreContainerWorkspaceContainerResponse>>
        GetContainerWorkspaceAsync(
            int? storeId,
            CancellationToken cancellationToken)
    {
        if (!storeId.HasValue)
        {
            return [];
        }

        var workspaces = await GetContainerWorkspacesAsync(
            [storeId.Value],
            cancellationToken);

        return workspaces.TryGetValue(storeId.Value, out var containers)
            ? containers
            : [];
    }

    private async Task<Dictionary<int, IReadOnlyList<StoreContainerWorkspaceContainerResponse>>>
        GetContainerWorkspacesAsync(
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

        var containerDefinitions = await dbContext.Containers
            .AsNoTracking()
            .Where(container =>
                container.CompanyId == companyId &&
                (container.IsActive ||
                 container.StoreContainers.Any(assignment =>
                     assignment.CompanyId == companyId &&
                     distinctStoreIds.Contains(assignment.StoreId) &&
                     assignment.IsActive)))
            .OrderBy(container => container.Name)
            .ThenBy(container => container.Id)
            .Select(container => new ContainerWorkspaceDefinition(
                container.Id,
                container.CompanyId,
                container.Code,
                container.Name,
                container.Description,
                container.IsActive))
            .ToListAsync(cancellationToken);

        var assignments = await dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                distinctStoreIds.Contains(assignment.StoreId) &&
                assignment.IsActive)
            .Select(assignment => new
            {
                assignment.StoreId,
                assignment.ContainerId,
                assignment.Id
            })
            .ToListAsync(cancellationToken);

        var assignmentByStoreAndContainer = assignments
            .GroupBy(assignment => assignment.StoreId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(assignment => assignment.ContainerId)
                    .ToDictionary(
                        containerGroup => containerGroup.Key,
                        containerGroup => containerGroup
                            .Select(assignment => assignment.Id)
                            .First()));

        var result = new Dictionary<
            int,
            IReadOnlyList<StoreContainerWorkspaceContainerResponse>>();

        foreach (var storeId in distinctStoreIds)
        {
            assignmentByStoreAndContainer.TryGetValue(
                storeId,
                out var assignmentByContainer);

            var containers = containerDefinitions
                .Select(container =>
                {
                    int? storeContainerId = null;
                    if (assignmentByContainer is not null &&
                        assignmentByContainer.TryGetValue(
                            container.Id,
                            out var assignmentId))
                    {
                        storeContainerId = assignmentId;
                    }

                    return new StoreContainerWorkspaceContainerResponse(
                        container.Id,
                        container.CompanyId,
                        container.Code,
                        container.Name,
                        container.Description,
                        container.IsActive,
                        storeContainerId.HasValue,
                        storeContainerId);
                })
                .ToList();

            result[storeId] = containers;
        }

        return result;
    }

    private sealed record ContainerWorkspaceDefinition(
        int Id,
        int CompanyId,
        string Code,
        string Name,
        string? Description,
        bool IsActive);

    public async Task<Result<BusinessPartnerResponse>> AddAsync(
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var partner = request.Adapt<BusinessPartner>();
        partner.CompanyId = companyId;

        var duplicateError = await FindDuplicateAsync(
            partner,
            excludedId: null,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateError);
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
        var duplicateError = await FindDuplicateAsync(
            normalizedPartner,
            id,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateError);
        }

        if (partner.Currency != normalizedPartner.Currency &&
            await HasFinancialRecordsAsync(id, cancellationToken))
        {
            return Result<BusinessPartnerResponse>.Failure(
                CurrencyChangeNotAllowed());
        }

        request.Adapt(partner);
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

    private async Task<Error?> FindDuplicateAsync(
        BusinessPartner partner,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = partner.Name.ToUpperInvariant();
        var normalizedCode = partner.Code.ToUpperInvariant();
        var normalizedTaxNumber = partner.TaxNumber?.ToUpperInvariant();

        var duplicates = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                (entity.Name.ToUpper() == normalizedName ||
                 entity.Code.ToUpper() == normalizedCode ||
                 (normalizedTaxNumber != null &&
                  entity.TaxNumber != null &&
                  entity.TaxNumber.ToUpper() == normalizedTaxNumber)))
            .Select(entity => new
            {
                entity.Name,
                entity.Code,
                entity.TaxNumber
            })
            .ToListAsync(cancellationToken);

        if (duplicates.Any(entity => string.Equals(
                entity.Name,
                partner.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "BusinessPartners.NameExists",
                $"اسم العميل أو المورد '{partner.Name}' موجود بالفعل.",
                nameof(BusinessPartnerRequest.Name));
        }

        if (duplicates.Any(entity => string.Equals(
                entity.Code,
                partner.Code,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "BusinessPartners.CodeExists",
                $"كود العميل أو المورد '{partner.Code}' مستخدم بالفعل.",
                nameof(BusinessPartnerRequest.Code));
        }

        return partner.TaxNumber is not null &&
               duplicates.Any(entity => string.Equals(
                   entity.TaxNumber,
                   partner.TaxNumber,
                   StringComparison.OrdinalIgnoreCase))
            ? Error.Conflict(
                "BusinessPartners.TaxNumberExists",
                "يوجد عميل أو مورد آخر يحمل الرقم الضريبي نفسه.",
                nameof(BusinessPartnerRequest.TaxNumber))
            : null;
    }

    private async Task<bool> HasFinancialRecordsAsync(
        int businessPartnerId,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.BusinessPartnerId == businessPartnerId,
                cancellationToken) ||
        await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.BusinessPartnerId == businessPartnerId,
                cancellationToken) ||
        await dbContext.BusinessPartnerMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.BusinessPartnerId == businessPartnerId,
                cancellationToken) ||
        await dbContext.ContainerMovements
            .IgnoreQueryFilters()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.BusinessPartnerId == businessPartnerId,
                cancellationToken) ||
        await dbContext.DriverTrips
            .IgnoreQueryFilters()
            .AnyAsync(
                trip =>
                    trip.CompanyId == companyId &&
                    trip.BusinessPartnerId == businessPartnerId,
                cancellationToken);

    private static Error InvalidId() =>
        Error.Validation(
            "BusinessPartners.InvalidId",
            "يجب أن يكون رقم العميل أو المورد أكبر من صفر.");

    private static Error ContainerStoreNotFound(int id) =>
        Error.NotFound(
            "BusinessPartners.ContainerStoreNotFound",
            $"لم يتم العثور على مخزن عبوات نشط مرتبط بالعميل أو المورد رقم {id}.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "BusinessPartners.NotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.");

    private static Error HasContainerStores() =>
        Error.Conflict(
            "BusinessPartners.HasContainerStores",
            "لا يمكن حذف العميل أو المورد لارتباطه بمخزن عبوات حالي أو تاريخي.");

    private static Error HasFinancialRecords() =>
        Error.Conflict(
            "BusinessPartners.HasFinancialRecords",
            "لا يمكن حذف العميل أو المورد لارتباطه بسجلات مالية حالية أو تاريخية.");

    private static Error CurrencyChangeNotAllowed() =>
        Error.Conflict(
            "BusinessPartners.CurrencyChangeNotAllowed",
            "لا يمكن تغيير عملة العميل أو المورد بعد إنشاء سجلات مالية مرتبطة به.",
            nameof(BusinessPartnerRequest.Currency));
}
