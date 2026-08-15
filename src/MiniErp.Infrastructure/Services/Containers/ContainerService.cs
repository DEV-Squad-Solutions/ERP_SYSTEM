using Mapster;
using static MiniErp.Application.Features.Containers.ContainerErrors;
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
        ContainerFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new ContainerFilterRequest();
        var query = dbContext.Containers
            .AsNoTracking()
            .Where(container => container.CompanyId == companyId)
            .Where(container =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                container.Code.Contains(filters.Search.Trim()) ||
                container.Name.Contains(filters.Search.Trim()) ||
                (container.Description != null &&
                 container.Description.Contains(filters.Search.Trim())))
            .Where(container =>
                string.IsNullOrWhiteSpace(filters.Code) ||
                container.Code.Contains(filters.Code.Trim()))
            .Where(container =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                container.Name.Contains(filters.Name.Trim()))
            .Where(container =>
                !filters.IsActive.HasValue ||
                container.IsActive == filters.IsActive.Value)
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
        container.Code = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "CNT",
            companyId: companyId,
            existingIdentifiers: dbContext.Containers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.Code),
            cancellationToken);

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

        var code = container.Code;
        request.Adapt(container);
        container.Code = code;
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

}
