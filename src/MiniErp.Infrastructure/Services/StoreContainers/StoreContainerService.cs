using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StoreContainers;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StoreContainers;

public sealed class StoreContainerService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IStoreContainerService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StoreContainerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId)
            .OrderBy(assignment => assignment.Store.Name)
            .ThenBy(assignment => assignment.Container.Name)
            .ThenBy(assignment => assignment.Id);

        return await paginationService.PaginateAsync<
            StoreContainer,
            StoreContainerResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        int storeId,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
        {
            return Result<IReadOnlyList<SelectResponse>>.Failure(
                InvalidStoreId());
        }

        var storeError = await ValidateStoreAsync(
            storeId,
            requireUsable: true,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<IReadOnlyList<SelectResponse>>.Failure(storeError);
        }

        var response = await dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                assignment.StoreId == storeId &&
                assignment.IsActive &&
                assignment.Container.IsActive)
            .OrderBy(assignment => assignment.Container.Name)
            .ThenBy(assignment => assignment.ContainerId)
            .Select(assignment => new SelectResponse(
                assignment.ContainerId,
                assignment.Container.Name))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<StoreContainerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StoreContainerResponse>.Failure(InvalidId());
        }

        var response = await dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment =>
                assignment.Id == id &&
                assignment.CompanyId == companyId)
            .ProjectToType<StoreContainerResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<StoreContainerResponse>.Failure(NotFound(id))
            : Result<StoreContainerResponse>.Success(response);
    }

    public async Task<Result<IReadOnlyList<StoreContainerResponse>>> UpsertAsync(
        StoreContainerUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StoreId <= 0)
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                InvalidStoreId());
        }

        if (request.ContainerIds is null)
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                ContainerIdsRequired());
        }

        if (request.ContainerIds.Count >
            StoreContainerUpsertRequest.MaximumContainerCount)
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                TooManyContainers());
        }

        if (request.ContainerIds.Any(id => id <= 0))
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                InvalidContainerId());
        }

        if (request.ContainerIds.Count != request.ContainerIds.Distinct().Count())
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                DuplicateContainerIds());
        }

        var requestedContainerIds = request.ContainerIds.ToArray();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var storeError = await ValidateStoreAsync(
            request.StoreId,
            requireUsable: requestedContainerIds.Length > 0,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                storeError);
        }

        var containerError = await ValidateContainersAsync(
            requestedContainerIds,
            cancellationToken);
        if (containerError is not null)
        {
            return Result<IReadOnlyList<StoreContainerResponse>>.Failure(
                containerError);
        }

        var requestedContainerIdSet = requestedContainerIds.ToHashSet();
        var assignments = await dbContext.StoreContainers
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                assignment.StoreId == request.StoreId)
            .OrderBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        var existingContainerIds = assignments
            .Select(assignment => assignment.ContainerId)
            .ToHashSet();

        foreach (var group in assignments.GroupBy(
                     assignment => assignment.ContainerId))
        {
            if (!requestedContainerIdSet.Contains(group.Key))
            {
                foreach (var assignment in group)
                {
                    SoftDelete(assignment);
                }

                continue;
            }

            var assignmentToKeep =
                group.FirstOrDefault(assignment => assignment.IsActive) ??
                group.First();
            assignmentToKeep.IsActive = true;

            foreach (var duplicate in group.Where(
                         assignment => !ReferenceEquals(
                             assignment,
                             assignmentToKeep)))
            {
                SoftDelete(duplicate);
            }
        }

        foreach (var containerId in requestedContainerIds.Where(
                     id => !existingContainerIds.Contains(id)))
        {
            dbContext.StoreContainers.Add(new StoreContainer
            {
                CompanyId = companyId,
                StoreId = request.StoreId,
                ContainerId = containerId,
                IsActive = true
            });
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = await dbContext.StoreContainers
            .AsNoTracking()
            .Where(assignment =>
                assignment.CompanyId == companyId &&
                assignment.StoreId == request.StoreId &&
                assignment.IsActive)
            .OrderBy(assignment => assignment.Container.Name)
            .ThenBy(assignment => assignment.ContainerId)
            .ProjectToType<StoreContainerResponse>()
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<IReadOnlyList<StoreContainerResponse>>.Success(response);
    }

    private async Task<Error?> ValidateContainersAsync(
        IReadOnlyList<int> containerIds,
        CancellationToken cancellationToken)
    {
        if (containerIds.Count == 0)
        {
            return null;
        }

        var containers = await dbContext.Containers
            .AsNoTracking()
            .Where(container =>
                container.CompanyId == companyId &&
                containerIds.Contains(container.Id))
            .Select(container => new
            {
                container.Id,
                container.IsActive
            })
            .ToListAsync(cancellationToken);

        var containersById = containers.ToDictionary(container => container.Id);
        var missingIds = containerIds
            .Where(id => !containersById.ContainsKey(id))
            .ToArray();
        if (missingIds.Length > 0)
        {
            return Error.NotFound(
                "StoreContainers.ContainerNotFound",
                $"لم يتم العثور على العبوات ذات الأرقام: " +
                $"{string.Join(", ", missingIds)}.",
                nameof(StoreContainerUpsertRequest.ContainerIds));
        }

        var inactiveIds = containerIds
            .Where(id => !containersById[id].IsActive)
            .ToArray();

        return inactiveIds.Length == 0
            ? null
            : Error.Conflict(
                "StoreContainers.ContainerInactive",
                $"يجب اختيار عبوات نشطة. العبوات غير النشطة: " +
                $"{string.Join(", ", inactiveIds)}.",
                nameof(StoreContainerUpsertRequest.ContainerIds));
    }

    private void SoftDelete(StoreContainer assignment)
    {
        assignment.IsActive = false;
        dbContext.StoreContainers.Remove(assignment);
    }

    private async Task<Error?> ValidateStoreAsync(
        int storeId,
        bool requireUsable,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == storeId)
            .Select(entity => new
            {
                entity.IsActive,
                entity.IsContainerStore,
                BusinessPartnerIsActive = entity.BusinessPartner != null &&
                    entity.BusinessPartner.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (store is null)
        {
            return Error.NotFound(
                "StoreContainers.StoreNotFound",
                $"لم يتم العثور على المخزن رقم {storeId}.",
                nameof(StoreContainerUpsertRequest.StoreId));
        }

        if (!store.IsContainerStore)
        {
            return Error.Conflict(
                "StoreContainers.StoreNotContainerStore",
                "يجب اختيار مخزن عبوات وليس مخزن منتجات.",
                nameof(StoreContainerUpsertRequest.StoreId));
        }

        if (!requireUsable)
        {
            return null;
        }

        if (!store.IsActive)
        {
            return Error.Conflict(
                "StoreContainers.StoreInactive",
                "يجب اختيار مخزن عبوات نشط.",
                nameof(StoreContainerUpsertRequest.StoreId));
        }

        return store.BusinessPartnerIsActive
            ? null
            : Error.Conflict(
                "StoreContainers.StoreBusinessPartnerInactive",
                "يجب أن يكون العميل أو المورد المرتبط بمخزن العبوات نشطًا.",
                nameof(StoreContainerUpsertRequest.StoreId));
    }

    private static Error InvalidId() =>
        Error.Validation(
            "StoreContainers.InvalidId",
            "يجب أن يكون رقم ربط العبوة بالمخزن أكبر من صفر.");

    private static Error InvalidStoreId() =>
        Error.Validation(
            "StoreContainers.InvalidStoreId",
            "يجب أن يكون رقم المخزن أكبر من صفر.",
            nameof(StoreContainerUpsertRequest.StoreId));

    private static Error ContainerIdsRequired() =>
        Error.Validation(
            "StoreContainers.ContainerIdsRequired",
            "حقل العبوات مطلوب.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    private static Error TooManyContainers() =>
        Error.Validation(
            "StoreContainers.TooManyContainers",
            $"يجب ألا يزيد عدد العبوات عن " +
            $"{StoreContainerUpsertRequest.MaximumContainerCount}.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    private static Error InvalidContainerId() =>
        Error.Validation(
            "StoreContainers.InvalidContainerId",
            "يجب أن تكون جميع أرقام العبوات أكبر من صفر.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    private static Error DuplicateContainerIds() =>
        Error.Validation(
            "StoreContainers.DuplicateContainerIds",
            "يجب عدم تكرار رقم العبوة في القائمة.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    private static Error NotFound(int id) =>
        Error.NotFound(
            "StoreContainers.NotFound",
            $"لم يتم العثور على ربط العبوة بالمخزن رقم {id}.");
}
