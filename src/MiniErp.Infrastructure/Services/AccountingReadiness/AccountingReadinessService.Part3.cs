using System.Data;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountingReadiness.AccountingReadinessErrors;

namespace MiniErp.Infrastructure.Services.AccountingReadiness;

public sealed partial class AccountingReadinessService
{
    private async Task AddSimpleRequirementsAsync(
        List<MappingRequirement> requirements,
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var hasStockAdjustments = await dbContext.StockAdjustments.AnyAsync(
            adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.FiscalYearId == fiscalYearId &&
                adjustment.DocumentDate >= startDate &&
                adjustment.DocumentDate <= endDate,
            cancellationToken);
        if (hasStockAdjustments)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Inventory,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.InventoryAdjustmentGain,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.InventoryAdjustmentLoss,
                null));
        }

        var hasStockOpenings = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                !balance.DocumentNumber.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.DocumentNumberPrefix) &&
                (balance.Notes == null || !balance.Notes.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.MarkerPrefix)) &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate,
            cancellationToken);
        var hasOtherOpenings = await dbContext.PartnerOpeningBalances.AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.FiscalYearId == fiscalYearId &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken) ||
            await dbContext.EmployeeOpeningBalances.AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.FiscalYearId == fiscalYearId &&
                    !balance.PayrollEntryId.HasValue &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken) ||
            await dbContext.Cashboxes.AnyAsync(
                cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.BaseOpeningBalance != 0m &&
                    cashbox.OpeningBalanceDate >= startDate &&
                    cashbox.OpeningBalanceDate <= endDate,
                cancellationToken);
        if (hasStockOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Inventory,
                null));
        }

        var hasDriverOpenings = await dbContext.DriverOpeningBalances.AnyAsync(balance =>
            balance.CompanyId == companyId && balance.FiscalYearId == fiscalYearId &&
            balance.DocumentDate >= startDate && balance.DocumentDate <= endDate, cancellationToken);
        if (hasStockOpenings || hasOtherOpenings || hasDriverOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.OpeningBalanceEquity,
                null));
        }

        var hasPartnerOpenings = await dbContext.PartnerOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate,
            cancellationToken);
        if (hasPartnerOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.CustomerControl,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.SupplierControl,
                null));
        }

        var hasEmployeeOpenings = await dbContext.EmployeeOpeningBalances
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.FiscalYearId == fiscalYearId &&
                    !balance.PayrollEntryId.HasValue &&
                    balance.DocumentDate >= startDate &&
                    balance.DocumentDate <= endDate,
                cancellationToken);
        if (hasEmployeeOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.EmployeeControl,
                null));
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.EmployeeReceivable,
                null));
        }

        var cashboxOpeningIds = await dbContext.Cashboxes
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.BaseOpeningBalance != 0m &&
                cashbox.OpeningBalanceDate >= startDate &&
                cashbox.OpeningBalanceDate <= endDate)
            .Select(cashbox => cashbox.Id)
            .ToListAsync(cancellationToken);
        requirements.AddRange(cashboxOpeningIds
            .Select(cashboxId => MappingRequirement.For(
                AccountingMappingType.Cashbox,
                cashboxId))
            .ToArray());

        var hasTrips = await dbContext.DriverTrips.AnyAsync(
            trip =>
                trip.CompanyId == companyId &&
                trip.FiscalYearId == fiscalYearId &&
                trip.Cost.HasValue &&
                trip.Cost.Value > 0m &&
                trip.TripDate >= startDate &&
                trip.TripDate <= endDate,
            cancellationToken);
        if (hasTrips)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.DriverTripExpense,
                null));
        }
        if (hasTrips || hasDriverOpenings)
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.DriverControl,
                null));
        }
    }

    private async Task<Result<FiscalYearSnapshot>> GetFiscalYearAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        if (fiscalYearId <= 0)
        {
            return Result<FiscalYearSnapshot>.Failure(
                InvalidFiscalYearId());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.Id == fiscalYearId)
            .Select(year => new FiscalYearSnapshot(
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate,
                year.Status))
            .SingleOrDefaultAsync(cancellationToken);
        return fiscalYear is null
            ? Result<FiscalYearSnapshot>.Failure(
                FiscalYearNotFound(fiscalYearId))
            : Result<FiscalYearSnapshot>.Success(fiscalYear);
    }

    private static AccountingReadinessIssue CreateSourceIssue(
        string issueType,
        SourceDescriptor source,
        string message) =>
        new(
            issueType,
            source.Key.SourceType,
            source.Key.SourceId,
            source.SourceNumber,
            source.SourceDate,
            null,
            null,
            message);

    private static JournalEntrySourceType? MapMovementSourceType(
        ItemMovementType movementType) =>
        movementType switch
        {
            ItemMovementType.Sales or
            ItemMovementType.SalesReturn or
            ItemMovementType.Purchase or
            ItemMovementType.PurchaseReturn => JournalEntrySourceType.Invoice,
            ItemMovementType.OpeningBalance =>
                JournalEntrySourceType.StockOpeningBalance,
            ItemMovementType.AdjustmentIncrease or
            ItemMovementType.AdjustmentDecrease =>
                JournalEntrySourceType.StockAdjustment,
            _ => null
        };

    private static decimal AllocateNetAmount(
        decimal lineSubtotal,
        decimal invoiceSubtotal,
        decimal invoiceDiscount) =>
        lineSubtotal <= 0m
            ? 0m
            : invoiceSubtotal <= 0m
                ? lineSubtotal
                : InventoryCostRules.RoundValue(
                    lineSubtotal -
                    InventoryCostRules.RoundValue(
                        invoiceDiscount * lineSubtotal / invoiceSubtotal));

    private static void RecordReadinessCheck(string result) =>
        ReadinessChecks.Add(
            1,
            new KeyValuePair<string, object?>("operation", "readiness"),
            new KeyValuePair<string, object?>("result", result));

    private static void RecordBackfill(string result) =>
        Backfills.Add(
            1,
            new KeyValuePair<string, object?>("operation", "backfill"),
            new KeyValuePair<string, object?>("result", result));

    private static void RecordFailure(string operation, string result) =>
        ReadinessFailures.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result));

    private sealed record FiscalYearSnapshot(
        int Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        FiscalYearStatus Status);

    private sealed record SourceKey(
        JournalEntrySourceType SourceType,
        int SourceId);

    private sealed record SourceDescriptor(
        SourceKey Key,
        string SourceNumber,
        DateOnly SourceDate);

    private sealed record MappingRequirement(
        AccountingMappingType MappingType,
        int? SourceId,
        JournalEntrySourceType? SourceType,
        int? SourceDocumentId,
        string? SourceNumber,
        DateOnly? SourceDate)
    {
        public static MappingRequirement For(
            AccountingMappingType mappingType,
            int? sourceId,
            JournalEntrySourceType? sourceType = null,
            int? sourceDocumentId = null,
            string? sourceNumber = null,
            DateOnly? sourceDate = null) =>
            new(
                mappingType,
                sourceId,
                sourceType,
                sourceDocumentId,
                sourceNumber,
                sourceDate);
    }
}
