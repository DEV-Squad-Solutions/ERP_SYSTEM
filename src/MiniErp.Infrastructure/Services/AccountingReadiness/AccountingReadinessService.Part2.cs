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
    private async Task<Result> SynchronizeAsync(
        SourceDescriptor source,
        CancellationToken cancellationToken)
    {
        switch (source.Key.SourceType)
        {
            case JournalEntrySourceType.Invoice:
                return ToResult(await invoicePostingService.SynchronizeAsync(
                    source.Key.SourceId,
                    cancellationToken));

            case JournalEntrySourceType.CashVoucher:
                var voucher = await dbContext.CashVouchers
                    .SingleAsync(
                        entity =>
                            entity.CompanyId == companyId &&
                            entity.Id == source.Key.SourceId,
                        cancellationToken);
                return ToResult(await cashVoucherPostingService.SynchronizeAsync(
                    voucher,
                    cancellationToken));

            case JournalEntrySourceType.CashboxTransfer:
                return ToResult(await cashboxTransferPostingService
                    .SynchronizeAsync(source.Key.SourceId, cancellationToken));

            case JournalEntrySourceType.StockAdjustment:
                return await inventoryPostingService
                    .SynchronizeStockAdjustmentAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.StockOpeningBalance:
                return await inventoryPostingService
                    .SynchronizeStockOpeningBalanceAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.PartnerOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizePartnerAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.EmployeeOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizeEmployeeAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.DriverOpeningBalance:
                return await openingBalancePostingService.SynchronizeDriverAsync(
                    source.Key.SourceId, cancellationToken);

            case JournalEntrySourceType.CashboxOpeningBalance:
                return await openingBalancePostingService
                    .SynchronizeCashboxAsync(
                        source.Key.SourceId,
                        cancellationToken);

            case JournalEntrySourceType.DriverTrip:
                return await driverTripPostingService.SynchronizeAsync(
                    source.Key.SourceId,
                    cancellationToken);

            case JournalEntrySourceType.CashboxRevaluation:
            case JournalEntrySourceType.MonetaryAccountRevaluation:
                return Result.Failure(
                    ImmutableRevaluationJournalMissing(
                        source.Key.SourceType.ToString(),
                        source.SourceNumber));

            default:
                return Result.Success();
        }
    }

    private static Result ToResult<T>(Result<T> result) =>
        result.IsFailure
            ? Result.Failure(result.Errors)
            : Result.Success();

    private async Task<List<SourceDescriptor>> LoadSourcesAsync(
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var sources = new List<SourceDescriptor>();

        sources.AddRange(await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.FiscalYearId == fiscalYearId &&
                invoice.InvoiceDate >= startDate &&
                invoice.InvoiceDate <= endDate &&
                (invoice.Total != 0m ||
                 invoice.BaseTotal != 0m ||
                 dbContext.ItemMovements.Any(movement =>
                     movement.CompanyId == companyId &&
                     movement.FiscalYearId == fiscalYearId &&
                     movement.ReferenceId == invoice.Id &&
                     movement.TotalCost > 0m &&
                     (movement.MovementType == ItemMovementType.Sales ||
                      movement.MovementType == ItemMovementType.SalesReturn)) ||
                 dbContext.InvoicePayments.Any(payment =>
                     payment.CompanyId == companyId &&
                     payment.FiscalYearId == fiscalYearId &&
                     payment.InvoiceId == invoice.Id &&
                     (payment.AppliedBaseAmount != 0m ||
                      payment.CashboxBaseAmount != 0m))))
            .Select(invoice => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.Invoice, invoice.Id),
                invoice.InvoiceNumber,
                invoice.InvoiceDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.FiscalYearId == fiscalYearId &&
                voucher.IsPosted &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue &&
                voucher.VoucherDate >= startDate &&
                voucher.VoucherDate <= endDate)
            .Select(voucher => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.CashVoucher, voucher.Id),
                voucher.VoucherNumber,
                voucher.VoucherDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.FiscalYearId == fiscalYearId &&
                transfer.TransferDate >= startDate &&
                transfer.TransferDate <= endDate)
            .Select(transfer => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.CashboxTransfer,
                    transfer.Id),
                transfer.TransferNumber,
                transfer.TransferDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.StockAdjustments
            .AsNoTracking()
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.FiscalYearId == fiscalYearId &&
                adjustment.DocumentDate >= startDate &&
                adjustment.DocumentDate <= endDate &&
                dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearId &&
                    movement.ReferenceId == adjustment.Id &&
                    (movement.MovementType ==
                        ItemMovementType.AdjustmentIncrease ||
                     movement.MovementType ==
                        ItemMovementType.AdjustmentDecrease) &&
                    movement.TotalCost > 0m))
            .Select(adjustment => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.StockAdjustment,
                    adjustment.Id),
                adjustment.DocumentNumber,
                adjustment.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                !balance.DocumentNumber.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.DocumentNumberPrefix) &&
                (balance.Notes == null || !balance.Notes.ToUpper().StartsWith(StockOpeningBalanceCarryForwardRules.MarkerPrefix)) &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate &&
                dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearId &&
                    movement.ReferenceId == balance.Id &&
                    movement.MovementType ==
                        ItemMovementType.OpeningBalance &&
                    movement.TotalCost > 0m))
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.StockOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                balance.BaseAmount != 0m &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate)
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.PartnerOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                !balance.PayrollEntryId.HasValue &&
                balance.BaseAmount != 0m &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate)
            .Select(balance => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.EmployeeOpeningBalance,
                    balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.DriverOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId &&
                balance.DocumentDate >= startDate &&
                balance.DocumentDate <= endDate)
            .Select(balance => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.DriverOpeningBalance, balance.Id),
                balance.DocumentNumber,
                balance.DocumentDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.BaseOpeningBalance != 0m &&
                cashbox.OpeningBalanceDate >= startDate &&
                cashbox.OpeningBalanceDate <= endDate)
            .Select(cashbox => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.CashboxOpeningBalance,
                    cashbox.Id),
                cashbox.Code,
                cashbox.OpeningBalanceDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.FiscalYearId == fiscalYearId &&
                trip.Cost.HasValue &&
                trip.Cost.Value > 0m &&
                trip.TripDate >= startDate &&
                trip.TripDate <= endDate)
            .Select(trip => new SourceDescriptor(
                new SourceKey(JournalEntrySourceType.DriverTrip, trip.Id),
                trip.InvoiceNumber ?? "TR-" + trip.Id,
                trip.TripDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.CashboxRevaluations
            .AsNoTracking()
            .Where(revaluation =>
                revaluation.CompanyId == companyId &&
                revaluation.FiscalYearId == fiscalYearId &&
                revaluation.DeltaBaseAmount != 0m &&
                revaluation.RevaluationDate >= startDate &&
                revaluation.RevaluationDate <= endDate)
            .Select(revaluation => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.CashboxRevaluation,
                    revaluation.Id),
                revaluation.Cashbox.Code,
                revaluation.RevaluationDate))
            .ToListAsync(cancellationToken));

        sources.AddRange(await dbContext.MonetaryAccountRevaluations
            .AsNoTracking()
            .Where(revaluation =>
                revaluation.CompanyId == companyId &&
                revaluation.FiscalYearId == fiscalYearId &&
                revaluation.DeltaBaseAmount != 0m &&
                revaluation.RevaluationDate >= startDate &&
                revaluation.RevaluationDate <= endDate)
            .Select(revaluation => new SourceDescriptor(
                new SourceKey(
                    JournalEntrySourceType.MonetaryAccountRevaluation,
                    revaluation.Id),
                revaluation.Account.Code,
                revaluation.RevaluationDate))
            .ToListAsync(cancellationToken));

        return sources
            .DistinctBy(source => source.Key)
            .ToList();
    }

    private static bool IsImmutableRevaluation(
        JournalEntrySourceType sourceType) =>
        sourceType is JournalEntrySourceType.CashboxRevaluation or
            JournalEntrySourceType.MonetaryAccountRevaluation;

    private Task<List<MissingOpeningLine>> LoadMissingOpeningLinesAsync(
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken) =>
        dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.CompanyId == companyId &&
                line.StockOpeningBalance.FiscalYearId == fiscalYearId &&
                line.StockOpeningBalance.DocumentDate >= startDate &&
                line.StockOpeningBalance.DocumentDate <= endDate &&
                !dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearId &&
                    movement.StoreId == line.StockOpeningBalance.StoreId &&
                    movement.ItemId == line.ItemId &&
                    movement.MovementType == ItemMovementType.OpeningBalance &&
                    movement.ReferenceId == line.StockOpeningBalanceId))
            .Select(line => new MissingOpeningLine
            {
                OpeningBalanceId = line.StockOpeningBalanceId,
                StoreId = line.StockOpeningBalance.StoreId,
                ItemId = line.ItemId,
                ItemUnitId = line.ItemUnitId,
                Quantity = line.Quantity,
                DocumentNumber = line.StockOpeningBalance.DocumentNumber,
                DocumentDate = line.StockOpeningBalance.DocumentDate
            })
            .ToListAsync(cancellationToken);

    private async Task<List<AccountingReadinessIssue>> LoadMappingIssuesAsync(
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var requirements = await LoadMappingRequirementsAsync(
            fiscalYearId,
            startDate,
            endDate,
            cancellationToken);
        if (requirements.Count == 0)
        {
            return [];
        }

        var mappings = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId)
            .Select(mapping => new
            {
                mapping.MappingType,
                mapping.SourceId,
                mapping.AccountId,
                mapping.Account.IsActive,
                mapping.Account.IsPosting
            })
            .ToListAsync(cancellationToken);

        var issues = new List<AccountingReadinessIssue>();
        foreach (var requirement in requirements.Distinct())
        {
            var mapping = mappings.FirstOrDefault(candidate =>
                candidate.MappingType == requirement.MappingType &&
                candidate.SourceId == requirement.SourceId);
            if (mapping is not null && mapping.IsActive && mapping.IsPosting)
            {
                continue;
            }

            issues.Add(new AccountingReadinessIssue(
                mapping is null ? "MissingMapping" : "InvalidMapping",
                requirement.SourceType,
                requirement.SourceDocumentId,
                requirement.SourceNumber,
                requirement.SourceDate,
                requirement.MappingType,
                requirement.SourceId,
                mapping is null
                    ? $"الربط المحاسبي {requirement.MappingType} غير موجود."
                    : $"الحساب المربوط بـ {requirement.MappingType} غير فعال أو غير قابل للتسجيل."));
        }

        return issues;
    }

    private async Task<List<MappingRequirement>> LoadMappingRequirementsAsync(
        int fiscalYearId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var requirements = new List<MappingRequirement>();
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.FiscalYearId == fiscalYearId &&
                invoice.InvoiceDate >= startDate &&
                invoice.InvoiceDate <= endDate &&
                (invoice.Total != 0m ||
                 invoice.BaseTotal != 0m ||
                 dbContext.ItemMovements.Any(movement =>
                     movement.CompanyId == companyId &&
                     movement.FiscalYearId == fiscalYearId &&
                     movement.ReferenceId == invoice.Id &&
                     movement.TotalCost > 0m &&
                     (movement.MovementType == ItemMovementType.Sales ||
                      movement.MovementType == ItemMovementType.SalesReturn)) ||
                 dbContext.InvoicePayments.Any(payment =>
                     payment.CompanyId == companyId &&
                     payment.FiscalYearId == fiscalYearId &&
                     payment.InvoiceId == invoice.Id &&
                     (payment.AppliedBaseAmount != 0m ||
                      payment.CashboxBaseAmount != 0m))))
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.InvoiceType,
                invoice.BaseSubtotal,
                invoice.BaseDiscountAmount,
                invoice.BaseTotal,
                IsItemInvoice = dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearId &&
                    movement.ReferenceId == invoice.Id &&
                    (movement.MovementType == ItemMovementType.Sales ||
                     movement.MovementType == ItemMovementType.SalesReturn ||
                     movement.MovementType == ItemMovementType.Purchase ||
                     movement.MovementType == ItemMovementType.PurchaseReturn)),
                HasItemLines = dbContext.InvoiceLines.Any(line =>
                    line.CompanyId == companyId &&
                    line.InvoiceId == invoice.Id &&
                    line.ItemId.HasValue),
                HasServiceLines = dbContext.InvoiceLines.Any(line =>
                    line.CompanyId == companyId &&
                    line.InvoiceId == invoice.Id &&
                    !line.ItemId.HasValue),
                ItemSubtotal = dbContext.InvoiceLines
                    .Where(line =>
                        line.CompanyId == companyId &&
                        line.InvoiceId == invoice.Id &&
                        line.ItemId.HasValue)
                    .Sum(line => (decimal?)line.BaseTotal) ?? 0m,
                HasCost = dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == fiscalYearId &&
                    movement.ReferenceId == invoice.Id &&
                    movement.TotalCost > 0m &&
                    (movement.MovementType == ItemMovementType.Sales ||
                     movement.MovementType == ItemMovementType.SalesReturn)),
                PurchaseReturnCarryingCost = dbContext.ItemMovements
                    .Where(movement =>
                        movement.CompanyId == companyId &&
                        movement.FiscalYearId == fiscalYearId &&
                        movement.ReferenceId == invoice.Id &&
                        movement.MovementType == ItemMovementType.PurchaseReturn)
                    .Sum(movement => (decimal?)movement.TotalCost) ?? 0m,
                HasPendingPurchaseReturnCost = dbContext.ItemMovements.Any(
                    movement =>
                        movement.CompanyId == companyId &&
                        movement.FiscalYearId == fiscalYearId &&
                        movement.ReferenceId == invoice.Id &&
                        movement.MovementType ==
                            ItemMovementType.PurchaseReturn &&
                        (movement.CostStatus ==
                            InventoryCostStatus.Pending ||
                         movement.CostStatus ==
                            InventoryCostStatus.PartiallyCosted))
            })
            .ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            var (invoiceMapping, controlMapping) = invoice.InvoiceType switch
            {
                InvoiceType.Sales => (
                    AccountingMappingType.Sales,
                    AccountingMappingType.CustomerControl),
                InvoiceType.SalesReturn => (
                    AccountingMappingType.SalesReturn,
                    AccountingMappingType.CustomerControl),
                InvoiceType.Purchase => (
                    AccountingMappingType.Purchase,
                    AccountingMappingType.SupplierControl),
                _ => (
                    AccountingMappingType.PurchaseReturn,
                    AccountingMappingType.SupplierControl)
            };
            var hasItemLines = invoice.HasItemLines || invoice.IsItemInvoice;
            var hasServiceLines = invoice.HasServiceLines;
            if (hasItemLines)
            {
                if (invoice.InvoiceType is InvoiceType.Purchase or
                    InvoiceType.PurchaseReturn)
                {
                    invoiceMapping = AccountingMappingType.Inventory;
                }

                requirements.Add(MappingRequirement.For(
                    invoiceMapping,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
            }

            if (hasServiceLines)
            {
                var serviceMapping = invoice.InvoiceType switch
                {
                    InvoiceType.Sales => AccountingMappingType.ServiceSales,
                    InvoiceType.SalesReturn =>
                        AccountingMappingType.ServiceSalesReturn,
                    InvoiceType.Purchase => AccountingMappingType.ServicePurchase,
                    _ => AccountingMappingType.ServicePurchaseReturn
                };
                requirements.Add(MappingRequirement.For(
                    serviceMapping,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
            }

            if (!hasItemLines && !hasServiceLines)
            {
                requirements.Add(MappingRequirement.For(
                    invoiceMapping,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
            }

            requirements.Add(MappingRequirement.For(
                controlMapping,
                null,
                JournalEntrySourceType.Invoice,
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate));
            if (invoice.HasCost)
            {
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.Inventory,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.CostOfGoodsSold,
                    null,
                    JournalEntrySourceType.Invoice,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate));
            }

            if (invoice.IsItemInvoice &&
                invoice.InvoiceType == InvoiceType.PurchaseReturn &&
                !invoice.HasPendingPurchaseReturnCost)
            {
                var itemAmount = AllocateNetAmount(
                    invoice.ItemSubtotal,
                    invoice.BaseSubtotal > 0m
                        ? invoice.BaseSubtotal
                        : invoice.ItemSubtotal,
                    invoice.BaseDiscountAmount);
                var difference = itemAmount -
                    invoice.PurchaseReturnCarryingCost;
                if (difference != 0m)
                {
                    requirements.Add(MappingRequirement.For(
                        difference > 0m
                            ? AccountingMappingType.InventoryAdjustmentGain
                            : AccountingMappingType.InventoryAdjustmentLoss,
                        null,
                        JournalEntrySourceType.Invoice,
                        invoice.Id,
                        invoice.InvoiceNumber,
                        invoice.InvoiceDate));
                }
            }
        }

        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.FiscalYearId == fiscalYearId &&
                voucher.IsPosted &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue &&
                voucher.VoucherDate >= startDate &&
                voucher.VoucherDate <= endDate)
            .Select(voucher => new
            {
                voucher.Id,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.Direction,
                voucher.CashboxId,
                voucher.PartyType,
                voucher.CashMovementTypeId,
                voucher.AccountId
            })
            .ToListAsync(cancellationToken);
        foreach (var voucher in vouchers)
        {
            if (voucher.CashboxId.HasValue)
            {
                requirements.Add(MappingRequirement.For(
                    AccountingMappingType.Cashbox,
                    voucher.CashboxId,
                    JournalEntrySourceType.CashVoucher,
                    voucher.Id,
                    voucher.VoucherNumber,
                    voucher.VoucherDate));
            }

            if (voucher.AccountId.HasValue)
            {
                continue;
            }

            var mappingType = voucher.PartyType switch
            {
                CashPartyType.Partner when
                    voucher.Direction == CashDirection.Receipt =>
                    AccountingMappingType.CustomerControl,
                CashPartyType.Partner => AccountingMappingType.SupplierControl,
                CashPartyType.Driver => AccountingMappingType.DriverControl,
                CashPartyType.Employee => AccountingMappingType.EmployeeControl,
                _ => AccountingMappingType.CashMovementType
            };
            requirements.Add(MappingRequirement.For(
                mappingType,
                mappingType == AccountingMappingType.CashMovementType
                    ? voucher.CashMovementTypeId
                    : null,
                JournalEntrySourceType.CashVoucher,
                voucher.Id,
                voucher.VoucherNumber,
                voucher.VoucherDate));
        }

        var transferCashboxes = await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.FiscalYearId == fiscalYearId &&
                transfer.TransferDate >= startDate &&
                transfer.TransferDate <= endDate)
            .Select(transfer => new
            {
                transfer.SourceCashboxId,
                transfer.DestinationCashboxId
            })
            .ToListAsync(cancellationToken);
        foreach (var cashboxId in transferCashboxes
                     .SelectMany(transfer => new[]
                     {
                         transfer.SourceCashboxId,
                         transfer.DestinationCashboxId
                     })
                     .Distinct())
        {
            requirements.Add(MappingRequirement.For(
                AccountingMappingType.Cashbox,
                cashboxId));
        }

        await AddSimpleRequirementsAsync(
            requirements,
            fiscalYearId,
            startDate,
            endDate,
            cancellationToken);
        return requirements;
    }
}
