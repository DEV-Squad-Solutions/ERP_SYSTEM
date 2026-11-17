using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ContainerMovements;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.ContainerMovements.ContainerMovementErrors;

namespace MiniErp.Infrastructure.Services.ContainerMovements;

public sealed class ContainerMovementService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard fiscalYearPeriodGuard)
    : IContainerMovementService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<ContainerMovementResponse>> CreateAsync(
        ContainerMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ContainerStoreId <= 0)
        {
            return Result<ContainerMovementResponse>.Failure(
                InvalidContainerStoreId());
        }

        var requestError = ValidateFields(
            request.ContainerId,
            request.MovementDate,
            request.OutgoingUnits,
            request.IncomingUnits,
            request.Description);
        if (requestError is not null)
        {
            return Result<ContainerMovementResponse>.Failure(requestError);
        }

        var store = await LoadContainerStoreAsync(
            request.ContainerStoreId,
            cancellationToken);
        if (store is null)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreNotFound(request.ContainerStoreId));
        }

        if (!store.IsContainerStore)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreNotContainerStore());
        }

        if (!store.IsActive)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreInactive());
        }

        if (!store.BusinessPartnerId.HasValue)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreWithoutPartner());
        }

        var container = await LoadContainerAsync(
            request.ContainerId,
            cancellationToken);
        if (container is null)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerNotFound(request.ContainerId));
        }

        if (!container.IsActive)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerInactive());
        }

        var isAssigned = await dbContext.StoreContainers
            .AsNoTracking()
            .AnyAsync(
                assignment =>
                    assignment.CompanyId == companyId &&
                    assignment.StoreId == store.Id &&
                    assignment.ContainerId == container.Id &&
                    assignment.IsActive,
                cancellationToken);
        if (!isAssigned)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerNotAssigned());
        }

        var fiscalYearResult = await ResolveOpenFiscalYearAsync(
            request.MovementDate,
            nameof(ContainerMovementRequest.MovementDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<ContainerMovementResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var movement = new ContainerMovement
        {
            CompanyId = companyId,
            FiscalYearId = fiscalYearResult.Value,
            BusinessPartnerId = store.BusinessPartnerId.Value,
            ContainerStoreId = store.Id,
            ContainerId = container.Id,
            InvoiceId = null,
            InvoiceNumber = null,
            SourceType = ContainerMovementSourceType.Manual,
            MovementDate = request.MovementDate,
            OutgoingUnits = request.OutgoingUnits,
            IncomingUnits = request.IncomingUnits,
            Description = NormalizeDescription(request.Description)
        };

        dbContext.ContainerMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ContainerMovementResponse>.Success(
            await ProjectResponseAsync(movement.Id, cancellationToken));
    }

    public async Task<Result<ContainerMovementResponse>> UpdateAsync(
        int id,
        ContainerMovementUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ContainerMovementResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: > 0 })
        {
            return Result<ContainerMovementResponse>.Failure(
                RowVersionRequired());
        }

        var requestError = ValidateFields(
            request.ContainerId,
            request.MovementDate,
            request.OutgoingUnits,
            request.IncomingUnits,
            request.Description);
        if (requestError is not null)
        {
            return Result<ContainerMovementResponse>.Failure(requestError);
        }

        var movement = await dbContext.ContainerMovements
            .FirstOrDefaultAsync(
                entity =>
                    entity.Id == id &&
                    entity.CompanyId == companyId,
                cancellationToken);
        if (movement is null)
        {
            return Result<ContainerMovementResponse>.Failure(NotFound(id));
        }

        if (movement.SourceType != ContainerMovementSourceType.Manual ||
            movement.InvoiceId.HasValue)
        {
            return Result<ContainerMovementResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (!movement.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<ContainerMovementResponse>.Failure(Concurrency());
        }

        var store = await LoadContainerStoreAsync(
            movement.ContainerStoreId,
            cancellationToken);
        if (store is null)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreNotFound(movement.ContainerStoreId));
        }

        if (!store.IsContainerStore)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreNotContainerStore());
        }

        if (!store.BusinessPartnerId.HasValue)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerStoreWithoutPartner());
        }

        var container = await LoadContainerAsync(
            request.ContainerId,
            cancellationToken);
        if (container is null)
        {
            return Result<ContainerMovementResponse>.Failure(
                ContainerNotFound(request.ContainerId));
        }

        var currentFiscalYear = await fiscalYearPeriodGuard
            .EnsureOpenForFiscalYearAsync(
                movement.FiscalYearId,
                movement.MovementDate,
                nameof(ContainerMovementUpdateRequest.MovementDate),
                cancellationToken);
        if (currentFiscalYear.IsFailure)
        {
            return Result<ContainerMovementResponse>.Failure(
                currentFiscalYear.Errors);
        }

        var fiscalYearResult = await ResolveOpenFiscalYearAsync(
            request.MovementDate,
            nameof(ContainerMovementUpdateRequest.MovementDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<ContainerMovementResponse>.Failure(
                fiscalYearResult.Errors);
        }

        movement.FiscalYearId = fiscalYearResult.Value;
        movement.BusinessPartnerId = store.BusinessPartnerId.Value;
        movement.ContainerId = container.Id;
        movement.MovementDate = request.MovementDate;
        movement.OutgoingUnits = request.OutgoingUnits;
        movement.IncomingUnits = request.IncomingUnits;
        movement.Description = NormalizeDescription(request.Description);

        dbContext.Entry(movement)
            .Property(entity => entity.RowVersion)
            .OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<ContainerMovementResponse>.Failure(Concurrency());
        }

        return Result<ContainerMovementResponse>.Success(
            await ProjectResponseAsync(movement.Id, cancellationToken));
    }

    public async Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        if (rowVersion is not { Length: > 0 })
        {
            return Result.Failure(RowVersionRequired());
        }

        var movement = await dbContext.ContainerMovements
            .FirstOrDefaultAsync(
                entity =>
                    entity.Id == id &&
                    entity.CompanyId == companyId,
                cancellationToken);
        if (movement is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (movement.SourceType != ContainerMovementSourceType.Manual ||
            movement.InvoiceId.HasValue)
        {
            return Result.Failure(InvoiceGeneratedReadOnly());
        }

        if (!movement.RowVersion.SequenceEqual(rowVersion))
        {
            return Result.Failure(Concurrency());
        }

        var fiscalYearResult = await fiscalYearPeriodGuard
            .EnsureOpenForFiscalYearAsync(
                movement.FiscalYearId,
                movement.MovementDate,
                nameof(ContainerMovementRequest.MovementDate),
                cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        dbContext.ContainerMovements.Remove(movement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        return Result.Success();
    }

    private async Task<Store?> LoadContainerStoreAsync(
        int storeId,
        CancellationToken cancellationToken) =>
        await dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(
                store =>
                    store.Id == storeId &&
                    store.CompanyId == companyId,
                cancellationToken);

    private async Task<Container?> LoadContainerAsync(
        int containerId,
        CancellationToken cancellationToken) =>
        await dbContext.Containers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                container =>
                    container.Id == containerId &&
                    container.CompanyId == companyId,
                cancellationToken);

    private async Task<Result<int>> ResolveOpenFiscalYearAsync(
        DateOnly movementDate,
        string fieldName,
        CancellationToken cancellationToken)
    {
        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: null,
            fromDate: movementDate,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<int>.Failure(fiscalYearResult.Errors);
        }

        var periodResult = await fiscalYearPeriodGuard
            .EnsureOpenForFiscalYearAsync(
                fiscalYearResult.Value.FiscalYearId,
                movementDate,
                fieldName,
                cancellationToken);
        if (periodResult.IsFailure)
        {
            return Result<int>.Failure(periodResult.Errors);
        }

        return Result<int>.Success(fiscalYearResult.Value.FiscalYearId);
    }

    private async Task<ContainerMovementResponse> ProjectResponseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.ContainerMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Id == id &&
                movement.CompanyId == companyId)
            .Select(movement => new
            {
                movement.Id,
                movement.BusinessPartnerId,
                movement.ContainerStoreId,
                movement.ContainerId,
                ContainerCode = movement.Container.Code,
                ContainerName = movement.Container.Name,
                movement.MovementDate,
                movement.OutgoingUnits,
                movement.IncomingUnits,
                movement.Description,
                movement.SourceType,
                movement.InvoiceId,
                movement.InvoiceNumber,
                movement.RowVersion
            })
            .FirstAsync(cancellationToken);

        return new ContainerMovementResponse(
            Id: row.Id,
            BusinessPartnerId: row.BusinessPartnerId,
            ContainerStoreId: row.ContainerStoreId,
            ContainerId: row.ContainerId,
            ContainerCode: row.ContainerCode,
            ContainerName: row.ContainerName,
            MovementDate: row.MovementDate,
            OutgoingUnits: row.OutgoingUnits,
            IncomingUnits: row.IncomingUnits,
            NetUnits: row.OutgoingUnits - row.IncomingUnits,
            Description: row.Description,
            SourceType: row.SourceType,
            InvoiceId: row.InvoiceId,
            InvoiceNumber: row.InvoiceNumber,
            RowVersion: row.RowVersion);
    }

    private static Error? ValidateFields(
        int containerId,
        DateOnly movementDate,
        int outgoingUnits,
        int incomingUnits,
        string? description)
    {
        if (containerId <= 0)
        {
            return InvalidContainerId();
        }

        if (movementDate == default)
        {
            return InvalidMovementDate();
        }

        if (outgoingUnits < 0 || incomingUnits < 0)
        {
            return UnitsNegative();
        }

        if (outgoingUnits == 0 && incomingUnits == 0)
        {
            return UnitsRequired();
        }

        if (description is not null &&
            description.Trim().Length > ContainerMovementRequest.DescriptionMaximumLength)
        {
            return DescriptionTooLong();
        }

        return null;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
