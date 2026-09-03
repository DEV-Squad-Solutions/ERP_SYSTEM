using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.StockOpeningBalances;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed class InventoryPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : IInventoryPostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> SynchronizeStockAdjustmentAsync(
        int stockAdjustmentId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.StockAdjustments
            .AsNoTracking()
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.Id == stockAdjustmentId)
            .Select(adjustment => new
            {
                adjustment.Id,
                adjustment.DocumentNumber,
                adjustment.DocumentDate,
                adjustment.Direction
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return Result.Failure(
                StockAdjustmentErrors.NotFound(stockAdjustmentId));
        }

        var fiscalYearResult = await ResolveFiscalYearAsync(
            document.DocumentDate,
            nameof(document.DocumentDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        var movementType = document.Direction ==
            StockAdjustmentDirection.Increase
                ? ItemMovementType.AdjustmentIncrease
                : ItemMovementType.AdjustmentDecrease;
        var amount = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.ReferenceId == document.Id &&
                movement.MovementType == movementType)
            .SumAsync(
                movement => (decimal?)movement.TotalCost,
                cancellationToken) ?? 0m;

        return await SynchronizeInventoryValueAsync(
            fiscalYearResult.Value,
            document.DocumentDate,
            document.DocumentNumber,
            JournalEntrySourceType.StockAdjustment,
            document.Id,
            document.Direction == StockAdjustmentDirection.Increase,
            amount,
            cancellationToken);
    }

    public async Task<Result> SynchronizeStockOpeningBalanceAsync(
        int stockOpeningBalanceId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(openingBalance =>
                openingBalance.CompanyId == companyId &&
                openingBalance.Id == stockOpeningBalanceId)
            .Select(openingBalance => new
            {
                openingBalance.Id,
                openingBalance.DocumentNumber,
                openingBalance.DocumentDate
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return Result.Failure(
                StockOpeningBalanceErrors.NotFound(stockOpeningBalanceId));
        }

        var fiscalYearResult = await ResolveFiscalYearAsync(
            document.DocumentDate,
            nameof(document.DocumentDate),
            cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result.Failure(fiscalYearResult.Errors);
        }

        var amount = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.ReferenceId == document.Id &&
                movement.MovementType == ItemMovementType.OpeningBalance)
            .SumAsync(
                movement => (decimal?)movement.TotalCost,
                cancellationToken) ?? 0m;
        if (amount <= 0m)
        {
            return await DeleteAsync(
                JournalEntrySourceType.StockOpeningBalance,
                document.Id,
                cancellationToken);
        }

        var inventoryResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            AccountingMappingType.Inventory,
            cancellationToken: cancellationToken);
        if (inventoryResult.IsFailure)
        {
            return Result.Failure(inventoryResult.Errors);
        }

        var equityResult = await accountMappingResolver.ResolveAsync(
            fiscalYearResult.Value,
            AccountingMappingType.OpeningBalanceEquity,
            cancellationToken: cancellationToken);
        if (equityResult.IsFailure)
        {
            return Result.Failure(equityResult.Errors);
        }

        return await SaveAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYearResult.Value,
                EntryDate: document.DocumentDate,
                Description:
                    $"رصيد افتتاحي مخزون {document.DocumentNumber}",
                SourceType: JournalEntrySourceType.StockOpeningBalance,
                SourceId: document.Id,
                SourceNumber: document.DocumentNumber,
                Lines:
                [
                    new JournalEntryLineRequest(
                        inventoryResult.Value,
                        "قيمة المخزون الافتتاحية",
                        amount,
                        0m),
                    new JournalEntryLineRequest(
                        equityResult.Value,
                        "مقابل رصيد المخزون الافتتاحي",
                        0m,
                        amount)
                ]),
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            sourceType,
            sourceId,
            cancellationToken);

    private async Task<Result> SynchronizeInventoryValueAsync(
        int fiscalYearId,
        DateOnly documentDate,
        string documentNumber,
        JournalEntrySourceType sourceType,
        int sourceId,
        bool isIncrease,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return await DeleteAsync(
                sourceType,
                sourceId,
                cancellationToken);
        }

        var inventoryResult = await accountMappingResolver.ResolveAsync(
            fiscalYearId,
            AccountingMappingType.Inventory,
            cancellationToken: cancellationToken);
        if (inventoryResult.IsFailure)
        {
            return Result.Failure(inventoryResult.Errors);
        }

        var counterpartResult = await accountMappingResolver.ResolveAsync(
            fiscalYearId,
            isIncrease
                ? AccountingMappingType.InventoryAdjustmentGain
                : AccountingMappingType.InventoryAdjustmentLoss,
            cancellationToken: cancellationToken);
        if (counterpartResult.IsFailure)
        {
            return Result.Failure(counterpartResult.Errors);
        }

        var inventoryLine = new JournalEntryLineRequest(
            inventoryResult.Value,
            $"تسوية مخزون {documentNumber}",
            isIncrease ? amount : 0m,
            isIncrease ? 0m : amount);
        var counterpartLine = new JournalEntryLineRequest(
            counterpartResult.Value,
            $"مقابل تسوية المخزون {documentNumber}",
            isIncrease ? 0m : amount,
            isIncrease ? amount : 0m);

        return await SaveAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYearId,
                EntryDate: documentDate,
                Description: $"تسوية مخزون {documentNumber}",
                SourceType: sourceType,
                SourceId: sourceId,
                SourceNumber: documentNumber,
                Lines: [inventoryLine, counterpartLine]),
            cancellationToken);
    }

    private async Task<Result> SaveAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await automaticPostingService.CreateOrUpdateAsync(
            request,
            cancellationToken);
        return result.IsFailure
            ? Result.Failure(result.Errors)
            : Result.Success();
    }

    private async Task<Result<int>> ResolveFiscalYearAsync(
        DateOnly documentDate,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= documentDate &&
                year.EndDate >= documentDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result<int>.Failure(
                FiscalYearErrors.DateNotCovered(documentDate, propertyName));
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result<int>.Success(fiscalYear.Id)
            : Result<int>.Failure(
                FiscalYearErrors.Closed(
                    documentDate,
                    fiscalYear.Name,
                    propertyName));
    }
}
