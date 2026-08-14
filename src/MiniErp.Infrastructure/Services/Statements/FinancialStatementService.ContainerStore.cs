using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<ContainerStoreStatementResponse>>
        GetContainerStoreStatementAsync(
            PaginationRequest pagination,
            ContainerStoreStatementFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<ContainerStoreStatementResponse>.Failure(
                paginationError);
        }

        var partnerRaw = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.BusinessPartnerId)
            .Select(entity => new ContainerStorePartnerRaw
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                PhoneNumber = entity.PhoneNumber,
                Email = entity.Email,
                Address = entity.Address,
                TaxNumber = entity.TaxNumber,
                Currency = entity.Currency,
                IsActive = entity.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (partnerRaw is null)
        {
            return Result<ContainerStoreStatementResponse>.Failure(
                StatementErrors.ContainerStorePartnerNotFound(
                    filters.BusinessPartnerId));
        }

        var partner = new ContainerStorePartnerResponse(
            Id: partnerRaw.Id,
            Code: partnerRaw.Code,
            Name: partnerRaw.Name,
            PhoneNumber: partnerRaw.PhoneNumber,
            Email: partnerRaw.Email,
            Address: partnerRaw.Address,
            TaxNumber: partnerRaw.TaxNumber,
            Currency: partnerRaw.Currency,
            IsActive: partnerRaw.IsActive);

        var storeRaw = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.BusinessPartnerId == filters.BusinessPartnerId &&
                entity.IsContainerStore &&
                entity.IsActive)
            .OrderBy(entity => entity.Id)
            .Select(entity => new ContainerStoreHeaderRaw
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Address = entity.Address,
                IsActive = entity.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (storeRaw is null)
        {
            return Result<ContainerStoreStatementResponse>.Failure(
                StatementErrors.ContainerStoreNotFound(
                    filters.BusinessPartnerId));
        }

        var store = new ContainerStoreHeaderResponse(
            Id: storeRaw.Id,
            Code: storeRaw.Code,
            Name: storeRaw.Name,
            Address: storeRaw.Address,
            IsActive: storeRaw.IsActive);

        var baseMovements = dbContext.ContainerMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.BusinessPartnerId == filters.BusinessPartnerId &&
                movement.ContainerStoreId == store.Id)
            .Where(movement =>
                !filters.ContainerId.HasValue ||
                movement.ContainerId == filters.ContainerId.Value);

        var openingByContainer = filters.FromDate.HasValue
            ? await baseMovements
                .Where(movement =>
                    movement.MovementDate < filters.FromDate.Value)
                .GroupBy(movement => movement.ContainerId)
                .Select(group => new
                {
                    ContainerId = group.Key,
                    Units = group.Sum(movement =>
                        movement.OutgoingUnits - movement.IncomingUnits)
                })
                .ToDictionaryAsync(
                    row => row.ContainerId,
                    row => row.Units,
                    cancellationToken)
            : [];

        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var query = baseMovements
            .Where(movement =>
                !filters.FromDate.HasValue ||
                movement.MovementDate >= filters.FromDate.Value)
            .Where(movement =>
                !filters.ToDate.HasValue ||
                movement.MovementDate <= filters.ToDate.Value)
            .Where(movement =>
                !filters.InvoiceType.HasValue ||
                movement.Invoice.InvoiceType == filters.InvoiceType.Value)
            .Where(movement =>
                string.IsNullOrEmpty(invoiceNumber) ||
                movement.InvoiceNumber.Contains(invoiceNumber) ||
                (movement.Invoice.PartnerInvoiceNo != null &&
                 movement.Invoice.PartnerInvoiceNo.Contains(invoiceNumber)))
            .Where(movement =>
                !filters.Direction.HasValue ||
                (filters.Direction == ContainerMovementDirection.Outgoing
                    ? movement.OutgoingUnits > 0
                    : movement.IncomingUnits > 0))
            .Where(movement =>
                string.IsNullOrEmpty(search) ||
                movement.InvoiceNumber.Contains(search) ||
                (movement.Invoice.PartnerInvoiceNo != null &&
                 movement.Invoice.PartnerInvoiceNo.Contains(search)) ||
                movement.Container.Code.Contains(search) ||
                movement.Container.Name.Contains(search) ||
                (movement.Container.Description != null &&
                 movement.Container.Description.Contains(search)) ||
                movement.ContainerStore.Code.Contains(search) ||
                movement.ContainerStore.Name.Contains(search) ||
                (movement.Description != null &&
                 movement.Description.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Outgoing = group.Sum(movement => movement.OutgoingUnits),
                Incoming = group.Sum(movement => movement.IncomingUnits)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalOutgoing = totals?.Outgoing ?? 0;
        var totalIncoming = totals?.Incoming ?? 0;

        var periodByContainer = await query
            .GroupBy(movement => movement.ContainerId)
            .Select(group => new ContainerStoreContainerPeriodRaw
            {
                ContainerId = group.Key,
                OutgoingUnits = group.Sum(movement =>
                    movement.OutgoingUnits),
                IncomingUnits = group.Sum(movement =>
                    movement.IncomingUnits)
            })
            .ToDictionaryAsync(
                row => row.ContainerId,
                cancellationToken);

        var ordered = query
            .OrderBy(movement => movement.MovementDate)
            .ThenBy(movement => movement.CreatedOn)
            .ThenBy(movement => movement.InvoiceId)
            .ThenBy(movement => movement.ContainerId)
            .ThenBy(movement => movement.Id);
        var offset = GetOffset(pagination, totalCount);
        var precedingByContainer = offset == 0
            ? []
            : await ordered
                .Take(offset)
                .GroupBy(movement => movement.ContainerId)
                .Select(group => new
                {
                    ContainerId = group.Key,
                    Units = group.Sum(movement =>
                        movement.OutgoingUnits - movement.IncomingUnits)
                })
                .ToDictionaryAsync(
                    row => row.ContainerId,
                    row => row.Units,
                    cancellationToken);

        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .Select(movement => new ContainerStoreMovementRaw
                {
                    MovementId = movement.Id,
                    MovementDate = movement.MovementDate,
                    InvoiceId = movement.InvoiceId,
                    InvoiceNumber = movement.InvoiceNumber,
                    PartnerInvoiceNumber =
                        movement.Invoice.PartnerInvoiceNo,
                    InvoiceType = movement.Invoice.InvoiceType,
                    ContainerId = movement.ContainerId,
                    ContainerCode = movement.Container.Code,
                    ContainerName = movement.Container.Name,
                    ContainerDescription = movement.Container.Description,
                    IsContainerActive = movement.Container.IsActive,
                    IsCurrentlyAssignedToStore =
                        dbContext.StoreContainers.Any(assignment =>
                            assignment.CompanyId == companyId &&
                            assignment.StoreId == store.Id &&
                            assignment.ContainerId == movement.ContainerId &&
                            assignment.IsActive),
                    OutgoingUnits = movement.OutgoingUnits,
                    IncomingUnits = movement.IncomingUnits,
                    MovementDescription = movement.Description,
                    CreatedOn = movement.CreatedOn
                })
                .ToListAsync(cancellationToken);

        var runningByContainer = new Dictionary<int, int>();
        var items = pageRows.Select(row =>
        {
            if (!runningByContainer.TryGetValue(
                    row.ContainerId,
                    out var runningUnits))
            {
                runningUnits =
                    openingByContainer.GetValueOrDefault(row.ContainerId) +
                    precedingByContainer.GetValueOrDefault(row.ContainerId);
            }

            var netUnits = row.OutgoingUnits - row.IncomingUnits;
            runningUnits += netUnits;
            runningByContainer[row.ContainerId] = runningUnits;

            return new ContainerStoreStatementItemResponse(
                MovementId: row.MovementId,
                MovementDate: row.MovementDate,
                InvoiceId: row.InvoiceId,
                InvoiceNumber: row.InvoiceNumber,
                PartnerInvoiceNumber: row.PartnerInvoiceNumber,
                InvoiceType: row.InvoiceType,
                ContainerId: row.ContainerId,
                ContainerCode: row.ContainerCode,
                ContainerName: row.ContainerName,
                ContainerDescription: row.ContainerDescription,
                IsContainerActive: row.IsContainerActive,
                IsCurrentlyAssignedToStore:
                    row.IsCurrentlyAssignedToStore,
                OutgoingUnits: row.OutgoingUnits,
                IncomingUnits: row.IncomingUnits,
                NetUnits: netUnits,
                RunningBalanceUnits: runningUnits,
                MovementDescription: row.MovementDescription,
                CreatedOn: row.CreatedOn);
        }).ToList();

        var containerMetadata = await LoadContainerMetadataAsync(
            store.Id,
            baseMovements,
            query,
            filters,
            search,
            cancellationToken);
        var containerSummaries = containerMetadata
            .Select(container =>
            {
                var openingUnits = openingByContainer.GetValueOrDefault(
                    container.ContainerId);
                var period = periodByContainer.GetValueOrDefault(
                    container.ContainerId);
                var periodOutgoing = period?.OutgoingUnits ?? 0;
                var periodIncoming = period?.IncomingUnits ?? 0;
                var periodNet = periodOutgoing - periodIncoming;
                return new ContainerStoreContainerSummaryResponse(
                    ContainerId: container.ContainerId,
                    ContainerCode: container.ContainerCode,
                    ContainerName: container.ContainerName,
                    ContainerDescription: container.ContainerDescription,
                    IsContainerActive: container.IsContainerActive,
                    IsCurrentlyAssignedToStore:
                        container.IsCurrentlyAssignedToStore,
                    OpeningUnits: openingUnits,
                    PeriodOutgoingUnits: periodOutgoing,
                    PeriodIncomingUnits: periodIncoming,
                    PeriodNetUnits: periodNet,
                    ClosingUnits: openingUnits + periodNet);
            })
            .OrderBy(container => container.ContainerCode)
            .ThenBy(container => container.ContainerId)
            .ToList();

        var openingUnits = containerSummaries.Sum(container =>
            container.OpeningUnits);
        var netUnits = totalOutgoing - totalIncoming;
        return Result<ContainerStoreStatementResponse>.Success(
            new ContainerStoreStatementResponse(
                BusinessPartner: partner,
                ContainerStore: store,
                Items: items,
                Containers: containerSummaries,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new ContainerStoreStatementSummaryResponse(
                    OpeningUnits: openingUnits,
                    TotalOutgoingUnits: totalOutgoing,
                    TotalIncomingUnits: totalIncoming,
                    NetUnits: netUnits,
                    ClosingUnits: openingUnits + netUnits,
                    DistinctContainerCount: containerSummaries.Count,
                    MovementCount: totalCount)));
    }

    private async Task<IReadOnlyList<ContainerStoreContainerMetadataRaw>>
        LoadContainerMetadataAsync(
            int storeId,
            IQueryable<ContainerMovement> baseMovements,
            IQueryable<ContainerMovement>
                filteredMovements,
            ContainerStoreStatementFilterRequest filters,
            string? search,
            CancellationToken cancellationToken)
    {
        var hasMovementOnlyFilters =
            filters.InvoiceType.HasValue ||
            !string.IsNullOrWhiteSpace(filters.InvoiceNumber) ||
            filters.Direction.HasValue;

        var movementSource = hasMovementOnlyFilters ||
                             !string.IsNullOrEmpty(search)
            ? filteredMovements
            : baseMovements;
        var movementContainers = await movementSource
            .Select(movement => new ContainerStoreContainerMetadataRaw
            {
                ContainerId = movement.ContainerId,
                ContainerCode = movement.Container.Code,
                ContainerName = movement.Container.Name,
                ContainerDescription = movement.Container.Description,
                IsContainerActive = movement.Container.IsActive,
                IsCurrentlyAssignedToStore =
                    dbContext.StoreContainers.Any(assignment =>
                        assignment.CompanyId == companyId &&
                        assignment.StoreId == storeId &&
                        assignment.ContainerId == movement.ContainerId &&
                        assignment.IsActive)
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignedContainers = hasMovementOnlyFilters
            ? []
            : await dbContext.StoreContainers
                .AsNoTracking()
                .Where(assignment =>
                    assignment.CompanyId == companyId &&
                    assignment.StoreId == storeId)
                .Where(assignment =>
                    !filters.ContainerId.HasValue ||
                    assignment.ContainerId == filters.ContainerId.Value)
                .Where(assignment =>
                    string.IsNullOrEmpty(search) ||
                    assignment.Container.Code.Contains(search) ||
                    assignment.Container.Name.Contains(search) ||
                    (assignment.Container.Description != null &&
                     assignment.Container.Description.Contains(search)) ||
                    assignment.Store.Code.Contains(search) ||
                    assignment.Store.Name.Contains(search))
                .Select(assignment =>
                    new ContainerStoreContainerMetadataRaw
                    {
                        ContainerId = assignment.ContainerId,
                        ContainerCode = assignment.Container.Code,
                        ContainerName = assignment.Container.Name,
                        ContainerDescription =
                            assignment.Container.Description,
                        IsContainerActive = assignment.Container.IsActive,
                        IsCurrentlyAssignedToStore =
                            dbContext.StoreContainers.Any(current =>
                                current.CompanyId == companyId &&
                                current.StoreId == storeId &&
                                current.ContainerId ==
                                assignment.ContainerId &&
                                current.IsActive)
                    })
                .Distinct()
                .ToListAsync(cancellationToken);

        return movementContainers
            .Concat(assignedContainers)
            .GroupBy(container => container.ContainerId)
            .Select(group => group.First())
            .ToList();
    }

    private sealed class ContainerStoreMovementRaw
    {
        public int MovementId { get; init; }
        public DateOnly MovementDate { get; init; }
        public int InvoiceId { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public string? PartnerInvoiceNumber { get; init; }
        public InvoiceType InvoiceType { get; init; }
        public int ContainerId { get; init; }
        public string ContainerCode { get; init; } = string.Empty;
        public string ContainerName { get; init; } = string.Empty;
        public string? ContainerDescription { get; init; }
        public bool IsContainerActive { get; init; }
        public bool IsCurrentlyAssignedToStore { get; init; }
        public int OutgoingUnits { get; init; }
        public int IncomingUnits { get; init; }
        public string? MovementDescription { get; init; }
        public DateTime CreatedOn { get; init; }
    }

    private sealed class ContainerStoreContainerPeriodRaw
    {
        public int ContainerId { get; init; }
        public int OutgoingUnits { get; init; }
        public int IncomingUnits { get; init; }
    }

    private sealed class ContainerStoreContainerMetadataRaw
    {
        public int ContainerId { get; init; }
        public string ContainerCode { get; init; } = string.Empty;
        public string ContainerName { get; init; } = string.Empty;
        public string? ContainerDescription { get; init; }
        public bool IsContainerActive { get; init; }
        public bool IsCurrentlyAssignedToStore { get; init; }
    }

    private sealed class ContainerStorePartnerRaw
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public string? TaxNumber { get; init; }
        public CurrencyCode Currency { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class ContainerStoreHeaderRaw
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Address { get; init; }
        public bool IsActive { get; init; }
    }
}
