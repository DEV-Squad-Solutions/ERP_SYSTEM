using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockTransfers;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.StockTransfers.StockTransferErrors;

namespace MiniErp.Infrastructure.Services.StockTransfers;

public sealed partial class StockTransferService
{
    public async Task<Result<PagedResponse<StockTransferListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            StockTransferFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        if (ValidateFilters(filters) is { } filterError)
        {
            return Result<PagedResponse<StockTransferListResponse>>.Failure(
                filterError);
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<StockTransferListResponse>>.Failure(
                fiscalYear.Errors);
        }

        var search = filters.Search?.Trim();
        var query = dbContext.StockTransfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.FiscalYearId == fiscalYear.Value.FiscalYearId);

        var orderedQuery = query
            .Where(transfer => string.IsNullOrEmpty(search) ||
                transfer.DocumentNumber.Contains(search) ||
                transfer.SourceStore.Name.Contains(search) ||
                transfer.DestinationStore.Name.Contains(search))
            .Where(transfer => !filters.SourceStoreId.HasValue ||
                transfer.SourceStoreId == filters.SourceStoreId.Value)
            .Where(transfer => !filters.DestinationStoreId.HasValue ||
                transfer.DestinationStoreId == filters.DestinationStoreId.Value)
            .Where(transfer => !filters.ItemId.HasValue ||
                transfer.Lines.Any(line => line.ItemId == filters.ItemId.Value))
            .Where(transfer => !filters.FromDate.HasValue ||
                transfer.TransferDate >= filters.FromDate.Value)
            .Where(transfer => !filters.ToDate.HasValue ||
                transfer.TransferDate <= filters.ToDate.Value)
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.Id);

        return await paginationService.PaginateAsync<
            StockTransfer,
            StockTransferListResponse>(
                orderedQuery,
                pagination,
                cancellationToken);
    }

    public async Task<Result<StockTransferResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockTransferResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(fiscalYear.Errors);
        }

        var response = await BuildResponseAsync(
            id,
            cancellationToken,
            fiscalYear.Value.FiscalYearId);
        return response is null
            ? Result<StockTransferResponse>.Failure(NotFound(id))
            : Result<StockTransferResponse>.Success(response);
    }

    public async Task<Result<StockTransferResponse>> AddAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var shapeError = ValidateRequestShape(
            request.TransferDate,
            request.SourceStoreId,
            request.DestinationStoreId,
            request.Lines);
        if (shapeError is not null)
        {
            return Result<StockTransferResponse>.Failure(shapeError);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.TransferDate,
                nameof(StockTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<StockTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var preparation = await PrepareAsync(
            request.SourceStoreId,
            request.DestinationStoreId,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(preparation.Error);
        }

        var documentNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix: "STF",
                companyId: companyId,
                existingIdentifiers: dbContext.StockTransfers
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.DocumentNumber),
                cancellationToken);

        var sourceKeys = request.Lines
            .Select(line => new InventoryCostingKey(
                request.SourceStoreId,
                line.ItemId))
            .ToArray();
        var destinationKeys = request.Lines
            .Select(line => new InventoryCostingKey(
                request.DestinationStoreId,
                line.ItemId))
            .ToArray();
        await inventoryCostingService.LockAsync(
            sourceKeys.Concat(destinationKeys).ToArray(),
            cancellationToken);

        var stockError = await ValidateStockAsync(
            request.SourceStoreId,
            request.TransferDate,
            request.Lines,
            replacedMovement: null,
            $"إضافة تحويل المخزون {documentNumber}",
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockTransferResponse>.Failure(stockError);
        }

        var transfer = request.Adapt<StockTransfer>();
        transfer.CompanyId = companyId;
        transfer.DocumentNumber = documentNumber;
        transfer.Touch(timeProvider.GetUtcNow().UtcDateTime);
        AddLines(transfer, request.Lines, preparation.Value);
        dbContext.StockTransfers.Add(transfer);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            AddMovements(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);

            // The first save assigns the fiscal year that covers TransferDate.
            // Recalculate that year, not the current one: a transfer can be
            // dated in another open fiscal year.
            var costingError = await inventoryCostingService.RecalculateAsync(
                sourceKeys
                    .Select(key => key with
                    {
                        FiscalYearId = transfer.FiscalYearId
                    })
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDocumentNumberConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockTransferResponse>.Failure(
                DocumentNumberExists(documentNumber));
        }

        var response = await BuildResponseAsync(transfer.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<StockTransferResponse>.Success(response!);
    }

    public async Task<Result<StockTransferResponse>> UpdateAsync(
        int id,
        StockTransferUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockTransferResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<StockTransferResponse>.Failure(RowVersionRequired());
        }

        var shapeError = ValidateLines(request.TransferDate, request.Lines);
        if (shapeError is not null)
        {
            return Result<StockTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var transfer = await LoadForWriteAsync(id, cancellationToken);
        if (transfer is null)
        {
            return Result<StockTransferResponse>.Failure(NotFound(id));
        }

        if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<StockTransferResponse>.Failure(Concurrency());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: transfer.FiscalYearId,
            fromDate: request.TransferDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(fiscalYear.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                transfer.TransferDate,
                nameof(StockTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<StockTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.TransferDate,
                nameof(StockTransferUpdateRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<StockTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var preparation = await PrepareAsync(
            transfer.SourceStoreId,
            transfer.DestinationStoreId,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockTransferResponse>.Failure(preparation.Error);
        }

        var movements = await LoadMovementsAsync(
            transfer.Id,
            transfer.DocumentNumber,
            cancellationToken);
        var oldKeys = GetCostingKeys(movements);
        var newSourceKeys = request.Lines.Select(line =>
            new InventoryCostingKey(
                transfer.SourceStoreId,
                line.ItemId,
                transfer.FiscalYearId)).ToArray();
        var newDestinationKeys = request.Lines.Select(line =>
            new InventoryCostingKey(
                transfer.DestinationStoreId,
                line.ItemId,
                transfer.FiscalYearId)).ToArray();
        await inventoryCostingService.LockAsync(
            oldKeys.Concat(newSourceKeys).Concat(newDestinationKeys).ToArray(),
            cancellationToken);

        var sourceMovementReference = MovementReference(
            ItemMovementType.TransferOut,
            transfer.Id,
            transfer.DocumentNumber);
        var sourceStockError = await ValidateStockAsync(
            transfer.SourceStoreId,
            request.TransferDate,
            request.Lines,
            sourceMovementReference,
            $"تعديل تحويل المخزون {transfer.DocumentNumber}",
            cancellationToken);
        if (sourceStockError is not null)
        {
            return Result<StockTransferResponse>.Failure(sourceStockError);
        }

        var destinationStockError = await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: transfer.DestinationStoreId,
                MovementDate: request.TransferDate,
                IsInbound: true,
                Lines: request.Lines.Select(line =>
                    new InventoryStockLine(line.ItemId, line.Quantity)).ToArray(),
                ReplacedMovement: MovementReference(
                    ItemMovementType.TransferIn,
                    transfer.Id,
                    transfer.DocumentNumber),
                OperationDescription:
                    $"تعديل تحويل المخزون {transfer.DocumentNumber}",
                ErrorFieldName: nameof(StockTransferRequest.Lines)),
            cancellationToken);
        if (destinationStockError is not null)
        {
            return Result<StockTransferResponse>.Failure(destinationStockError);
        }

        dbContext.Entry(transfer).Property(entity => entity.RowVersion)
            .OriginalValue = request.RowVersion;
        request.Adapt(transfer);
        transfer.Touch(timeProvider.GetUtcNow().UtcDateTime);
        ReplaceLines(transfer, request.Lines, preparation.Value);
        ReconcileMovements(transfer, movements);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var sourceCostingError = await inventoryCostingService.RecalculateAsync(
                oldKeys.Where(key => key.StoreId == transfer.SourceStoreId)
                    .Concat(newSourceKeys)
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (sourceCostingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(sourceCostingError);
            }

            var destinationCostingError = await inventoryCostingService
                .RecalculateAsync(
                    oldKeys.Where(key =>
                            key.StoreId == transfer.DestinationStoreId)
                        .Concat(newDestinationKeys)
                        .Distinct()
                        .ToArray(),
                    cancellationToken);
            if (destinationCostingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<StockTransferResponse>.Failure(
                    destinationCostingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockTransferResponse>.Failure(Concurrency());
        }

        var response = await BuildResponseAsync(transfer.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<StockTransferResponse>.Success(response!);
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
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var transfer = await LoadForWriteAsync(id, cancellationToken);
        if (transfer is null)
        {
            return Result.Failure(NotFound(id));
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: transfer.FiscalYearId,
            fromDate: transfer.TransferDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result.Failure(fiscalYear.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                transfer.TransferDate,
                nameof(StockTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        var movements = await LoadMovementsAsync(
            transfer.Id,
            transfer.DocumentNumber,
            cancellationToken);
        var costingKeys = GetCostingKeys(movements);
        await inventoryCostingService.LockAsync(costingKeys, cancellationToken);

        var destinationStockError = await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: transfer.DestinationStoreId,
                MovementDate: transfer.TransferDate,
                IsInbound: true,
                Lines: [],
                ReplacedMovement: MovementReference(
                    ItemMovementType.TransferIn,
                    transfer.Id,
                    transfer.DocumentNumber),
                OperationDescription:
                    $"حذف تحويل المخزون {transfer.DocumentNumber}",
                ErrorFieldName: nameof(StockTransferRequest.Lines)),
            cancellationToken);
        if (destinationStockError is not null)
        {
            return Result.Failure(destinationStockError);
        }

        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockTransferLines.RemoveRange(transfer.Lines);
        dbContext.StockTransfers.Remove(transfer);
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
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
