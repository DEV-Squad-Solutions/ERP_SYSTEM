using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Containers;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Containers;

public sealed class ContainerService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IContainerService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ContainerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Containers
            .AsNoTracking()
            .Where(container => container.CompanyId == companyId)
            .OrderBy(container => container.Name)
            .ThenBy(container => container.Id);

        return await paginationService.PaginateAsync<Container, ContainerResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Containers
            .AsNoTracking()
            .Where(container =>
                container.CompanyId == companyId &&
                container.IsActive)
            .OrderBy(container => container.Name)
            .ThenBy(container => container.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<ContainerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ContainerResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Containers
            .AsNoTracking()
            .Where(container =>
                container.Id == id &&
                container.CompanyId == companyId)
            .ProjectToType<ContainerResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ContainerResponse>.Failure(NotFound(id))
            : Result<ContainerResponse>.Success(response);
    }

    public async Task<Result<ContainerResponse>> AddAsync(
        ContainerRequest request,
        CancellationToken cancellationToken = default)
    {
        var container = request.Adapt<Container>();
        container.CompanyId = companyId;

        if (await ActiveCodeExistsAsync(
                container,
                excludedId: null,
                cancellationToken))
        {
            return Result<ContainerResponse>.Failure(CodeExists(container.Code));
        }

        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ContainerResponse>.Success(
            container.Adapt<ContainerResponse>());
    }

    public async Task<Result<ContainerResponse>> UpdateAsync(
        int id,
        ContainerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ContainerResponse>.Failure(InvalidId());
        }

        var container = await dbContext.Containers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (container is null)
        {
            return Result<ContainerResponse>.Failure(NotFound(id));
        }

        var normalizedContainer = request.Adapt<Container>();
        normalizedContainer.CompanyId = companyId;
        if (await ActiveCodeExistsAsync(
                normalizedContainer,
                id,
                cancellationToken))
        {
            return Result<ContainerResponse>.Failure(
                CodeExists(normalizedContainer.Code));
        }

        request.Adapt(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ContainerResponse>.Success(
            container.Adapt<ContainerResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var container = await dbContext.Containers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (container is null)
        {
            return Result.Failure(NotFound(id));
        }

        var hasStoreAssignments = await dbContext.StoreContainers
            .IgnoreQueryFilters()
            .AnyAsync(
                assignment =>
                    assignment.CompanyId == companyId &&
                    assignment.ContainerId == id,
                cancellationToken);
        if (hasStoreAssignments)
        {
            return Result.Failure(HasStoreAssignments());
        }

        container.IsActive = false;
        dbContext.Containers.Remove(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<bool> ActiveCodeExistsAsync(
        Container container,
        int? excludedId,
        CancellationToken cancellationToken) =>
        container.IsActive
            ? dbContext.Containers.AsNoTracking().AnyAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.IsActive &&
                    entity.Code == container.Code &&
                    (!excludedId.HasValue || entity.Id != excludedId.Value),
                cancellationToken)
            : Task.FromResult(false);

    private static Error InvalidId() =>
        Error.Validation(
            "Containers.InvalidId",
            "يجب أن يكون رقم العبوة أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Containers.NotFound",
            $"لم يتم العثور على العبوة رقم {id}.");

    private static Error CodeExists(string code) =>
        Error.Conflict(
            "Containers.CodeExists",
            $"كود العبوة '{code}' مستخدم بالفعل في عبوة نشطة.",
            nameof(ContainerRequest.Code));

    private static Error HasStoreAssignments() =>
        Error.Conflict(
            "Containers.HasStoreAssignments",
            "لا يمكن حذف العبوة لارتباطها بمخزن عبوات حالي أو تاريخي.");
}
