using System.Data;
using static MiniErp.Application.Features.StockAdjustments.StockAdjustmentErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockAdjustments;

public sealed partial class StockAdjustmentService
{
    public async Task<Result<PagedResponse<StockAdjustmentListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            StockAdjustmentFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new StockAdjustmentFilterRequest();
        var filterError = ValidateFilters(filters);
        if (filterError is not null)
        {
            return Result<PagedResponse<StockAdjustmentListResponse>>.Failure(
                filterError);
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<StockAdjustmentListResponse>>.Failure(
                fiscalYear.Errors);
        }

        var documentNumber = filters.DocumentNumber?.Trim();
        var query = dbContext.StockAdjustments
            .AsNoTracking()
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.FiscalYearId == fiscalYear.Value.FiscalYearId);

        var orderedQuery = query
            .Where(adjustment =>
                string.IsNullOrEmpty(documentNumber) ||
                adjustment.DocumentNumber.Contains(documentNumber))
            .Where(adjustment =>
                !filters.StoreId.HasValue ||
                adjustment.StoreId == filters.StoreId.Value)
            .Where(adjustment =>
                !filters.Direction.HasValue ||
                adjustment.Direction == filters.Direction.Value)
            .Where(adjustment =>
                !filters.FromDate.HasValue ||
                adjustment.DocumentDate >= filters.FromDate.Value)
            .Where(adjustment =>
                !filters.ToDate.HasValue ||
                adjustment.DocumentDate <= filters.ToDate.Value)
            .OrderByDescending(adjustment => adjustment.DocumentDate)
            .ThenByDescending(adjustment => adjustment.Id);

        var result = await paginationService.PaginateAsync<
            StockAdjustment,
            StockAdjustmentListResponse>(
                orderedQuery,
                pagination,
                cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        var enrichedItems = await EnrichListResponsesAsync(
            result.Value.Items,
            cancellationToken);
        return Result<PagedResponse<StockAdjustmentListResponse>>.Success(
            result.Value with
            {
                Items = enrichedItems
            });
    }

    public async Task<Result<StockAdjustmentResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockAdjustmentResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(fiscalYear.Errors);
        }

        var response = await ProjectResponseQuery(
                id,
                fiscalYear.Value.FiscalYearId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (response is not null)
        {
            response = await EnrichResponseAsync(
                response,
                cancellationToken);
        }

        return response is null
            ? Result<StockAdjustmentResponse>.Failure(NotFound(id))
            : Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result<StockAdjustmentResponse>> AddAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = await fiscalYearPeriodGuard.EnsureOpenAsync(
            request.DocumentDate,
            nameof(StockAdjustmentRequest.DocumentDate),
            cancellationToken);
        if (period.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(period.Errors);
        }

        var requested = request.Adapt<StockAdjustment>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var preparation = await ValidateRequestAsync(
            requested.StoreId,
            requested.Direction,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(preparation.Error);
        }

        requested.DocumentNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix: "ADJ",
                companyId: companyId,
                existingIdentifiers: dbContext.StockAdjustments
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.DocumentNumber),
                cancellationToken);

        await inventoryCostingService.LockAsync(
            request.Lines
                .Select(line => new InventoryCostingKey(
                    requested.StoreId,
                    line.ItemId))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var stockError = await ValidateStockAsync(
            requested,
            request.Lines,
            replacedMovement: null,
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockAdjustmentResponse>.Failure(stockError);
        }

        requested.CompanyId = companyId;
        requested.SourceInventoryCountId = null;
        requested.Touch(timeProvider.GetUtcNow().UtcDateTime);
        AddLines(requested, request.Lines, preparation.Value);

        dbContext.StockAdjustments.Add(requested);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            AddMovements(requested);
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await inventoryCostingService.RecalculateAsync(
                GetCostingKeys(requested),
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockAdjustmentResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (inventoryPostingService is not null)
            {
                var postingResult = await inventoryPostingService
                    .SynchronizeStockAdjustmentAsync(
                        requested.Id,
                        cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<StockAdjustmentResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateException exception)
            when (IsDocumentNumberConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockAdjustmentResponse>.Failure(
                DocumentNumberExists(requested.DocumentNumber));
        }

        var response = await ProjectResponseQuery(requested.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await EnrichResponseAsync(response, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result<StockAdjustmentResponse>> UpdateAsync(
        int id,
        StockAdjustmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockAdjustmentResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<StockAdjustmentResponse>.Failure(
                RowVersionRequired());
        }

        var requested = request.Adapt<StockAdjustment>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var adjustment = await LoadForWriteAsync(id, cancellationToken);
        if (adjustment is null)
        {
            return Result<StockAdjustmentResponse>.Failure(NotFound(id));
        }

        if (adjustment.SourceInventoryCountId.HasValue)
        {
            return Result<StockAdjustmentResponse>.Failure(
                GeneratedAdjustmentImmutable());
        }

        if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<StockAdjustmentResponse>.Failure(Concurrency());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: adjustment.FiscalYearId,
            fromDate: request.DocumentDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(fiscalYear.Errors);
        }

        var currentPeriod = await fiscalYearPeriodGuard.EnsureOpenAsync(
            adjustment.DocumentDate,
            nameof(StockAdjustmentRequest.DocumentDate),
            cancellationToken);
        if (currentPeriod.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(
                currentPeriod.Errors);
        }

        var requestedPeriod = await fiscalYearPeriodGuard.EnsureOpenAsync(
            request.DocumentDate,
            nameof(StockAdjustmentUpdateRequest.DocumentDate),
            cancellationToken);
        if (requestedPeriod.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(
                requestedPeriod.Errors);
        }

        var preparation = await ValidateRequestAsync(
            requested.StoreId,
            requested.Direction,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(preparation.Error);
        }

        var replacedMovement = MovementReference(
            adjustment.Id,
            adjustment.DocumentNumber);
        var oldMovements = await LoadMovementsAsync(
            adjustment.Id,
            adjustment.DocumentNumber,
            cancellationToken);
        var oldCostingKeys = GetCostingKeys(oldMovements);
        await inventoryCostingService.LockAsync(
            oldCostingKeys
                .Concat(request.Lines.Select(line =>
                    new InventoryCostingKey(
                        requested.StoreId,
                        line.ItemId)))
                .Distinct()
                .ToArray(),
            cancellationToken);
        var stockError = await ValidateStockAsync(
            requested,
            request.Lines,
            replacedMovement,
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockAdjustmentResponse>.Failure(stockError);
        }

        var entry = dbContext.Entry(adjustment);
        entry.Property(item => item.RowVersion).OriginalValue =
            request.RowVersion;

        request.Adapt(adjustment);
        ReplaceLines(
            adjustment,
            request.Lines,
            preparation.Value);
        adjustment.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        ReconcileMovements(adjustment, oldMovements);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await inventoryCostingService.RecalculateAsync(
                oldCostingKeys
                    .Concat(GetCostingKeys(adjustment))
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockAdjustmentResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (inventoryPostingService is not null)
            {
                var postingResult = await inventoryPostingService
                    .SynchronizeStockAdjustmentAsync(
                        adjustment.Id,
                        cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<StockAdjustmentResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockAdjustmentResponse>.Failure(Concurrency());
        }
        catch (DbUpdateException exception)
            when (IsDocumentNumberConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockAdjustmentResponse>.Failure(
                DocumentNumberExists(adjustment.DocumentNumber));
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await EnrichResponseAsync(response, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var adjustment = await LoadForWriteAsync(id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure(NotFound(id));
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: adjustment.FiscalYearId,
            fromDate: adjustment.DocumentDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result.Failure(fiscalYear.Errors);
        }

        var period = await fiscalYearPeriodGuard.EnsureOpenAsync(
            adjustment.DocumentDate,
            nameof(StockAdjustmentRequest.DocumentDate),
            cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure(period.Errors);
        }

        if (adjustment.SourceInventoryCountId.HasValue)
        {
            return Result.Failure(GeneratedAdjustmentImmutable());
        }

        var movements = await LoadMovementsAsync(
            adjustment.Id,
            adjustment.DocumentNumber,
            cancellationToken);
        var costingKeys = GetCostingKeys(movements);
        await inventoryCostingService.LockAsync(
            costingKeys,
            cancellationToken);

        var stockError = await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                adjustment.StoreId,
                adjustment.DocumentDate,
                StockAdjustmentMovementRules.IsInbound(adjustment.Direction),
                Lines: [],
                MovementReference(
                    adjustment.Id,
                    adjustment.DocumentNumber),
                $"حذف تسوية المخزون {adjustment.DocumentNumber}",
                nameof(StockAdjustmentRequest.Lines)),
            cancellationToken);
        if (stockError is not null)
        {
            return Result.Failure(stockError);
        }

        var entry = dbContext.Entry(adjustment);
        adjustment.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;
        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockAdjustmentLines.RemoveRange(adjustment.Lines);
        dbContext.StockAdjustments.Remove(adjustment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await inventoryCostingService.RecalculateAsync(
                costingKeys,
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (inventoryPostingService is not null)
            {
                var postingResult = await inventoryPostingService.DeleteAsync(
                    JournalEntrySourceType.StockAdjustment,
                    adjustment.Id,
                    cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result.Failure(postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
