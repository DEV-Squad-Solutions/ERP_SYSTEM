using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Monitoring;
using static MiniErp.Application.Features.ProfitabilityReports.ProfitabilityReportErrors;

namespace MiniErp.Infrastructure.Services.ProfitabilityReports;

public sealed class ProfitabilityReportService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    ILogger<ProfitabilityReportService>? logger = null)
    : IProfitabilityReportService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InvoiceProfitabilityListResponse>> GetInvoicesAsync(
        PaginationRequest pagination,
        ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(pagination, filters);
        if (validationError is not null)
        {
            return Result<InvoiceProfitabilityListResponse>.Failure(
                validationError);
        }

        var report = await LoadReportDataAsync(
            filters,
            invoiceId: null,
            alignReturnsToSourceInvoice: true,
            cancellationToken);
        var linkedReturns = report.Lines
            .Where(line =>
                line.InvoiceType == InvoiceType.SalesReturn &&
                line.SourceInvoiceId.HasValue)
            .ToLookup(line => line.SourceInvoiceId!.Value);
        var groups = report.Lines
            .Where(line => line.InvoiceType == InvoiceType.Sales)
            .GroupBy(line => line.InvoiceId)
            .Select(group => BuildInvoice(
                group
                    .Concat(linkedReturns[group.Key])
                    .ToArray(),
                includeDetails: false))
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenByDescending(invoice => invoice.InvoiceId)
            .ToArray();
        var totalCount = groups.Length;
        var invoices = Paginate(groups, pagination);

        return Result<InvoiceProfitabilityListResponse>.Success(
            new InvoiceProfitabilityListResponse(
                IncludeReturns: true,
                BaseCurrency: report.BaseCurrency,
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                Invoices: invoices
                    .Select(ToListItemResponse)
                    .ToArray(),
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: TotalPages(totalCount, pagination.PageSize),
                Summary: report.Summary));
    }

    public async Task<Result<InvoiceProfitabilityResponse>>
        GetInvoiceDetailsAsync(
            int invoiceId,
            CancellationToken cancellationToken = default)
    {
        if (invoiceId <= 0)
        {
            return Result<InvoiceProfitabilityResponse>.Failure(
                InvalidFilter(
                    nameof(invoiceId),
                    "رقم الفاتورة غير صحيح."));
        }

        var report = await LoadReportDataAsync(
            new ProfitabilityReportFilterRequest(),
            invoiceId,
            alignReturnsToSourceInvoice: true,
            cancellationToken);
        var saleLines = report.Lines
            .Where(line =>
                line.InvoiceType == InvoiceType.Sales &&
                line.InvoiceId == invoiceId)
            .ToArray();
        if (saleLines.Length == 0)
        {
            return Result<InvoiceProfitabilityResponse>.Failure(
                InvoiceNotFound(invoiceId));
        }

        var linkedReturns = report.Lines
            .Where(line =>
                line.InvoiceType == InvoiceType.SalesReturn &&
                line.SourceInvoiceId == invoiceId);
        return Result<InvoiceProfitabilityResponse>.Success(
            BuildInvoice(
                saleLines.Concat(linkedReturns).ToArray(),
                includeDetails: true));
    }

    public async Task<Result<ItemProfitabilityListResponse>> GetItemsAsync(
        PaginationRequest pagination,
        ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(pagination, filters);
        if (validationError is not null)
        {
            return Result<ItemProfitabilityListResponse>.Failure(
                validationError);
        }

        var report = await LoadReportDataAsync(
            filters,
            invoiceId: null,
            alignReturnsToSourceInvoice: false,
            cancellationToken);
        var groups = report.Lines
            .GroupBy(line => line.ItemId)
            .Select(group => BuildItem(group.ToArray()))
            .OrderBy(item => !item.GrossProfit.HasValue)
            .ThenByDescending(item => item.GrossProfit)
            .ThenByDescending(item => item.NetRevenue)
            .ThenBy(item => item.ItemName)
            .ThenBy(item => item.ItemId)
            .ToArray();
        var totalCount = groups.Length;

        return Result<ItemProfitabilityListResponse>.Success(
            new ItemProfitabilityListResponse(
                IncludeReturns: true,
                BaseCurrency: report.BaseCurrency,
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                Items: Paginate(groups, pagination),
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: TotalPages(totalCount, pagination.PageSize),
                Summary: report.Summary));
    }

    private async Task<ProfitabilityReportData> LoadReportDataAsync(
        ProfitabilityReportFilterRequest filters,
        int? invoiceId,
        bool alignReturnsToSourceInvoice,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var query = dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.ItemId.HasValue &&
                line.Invoice.ContentType == InvoiceContentType.Items &&
                (line.Invoice.InvoiceType == InvoiceType.Sales ||
                 line.Invoice.InvoiceType == InvoiceType.SalesReturn));

        if (invoiceId.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.Sales &&
                    line.InvoiceId == invoiceId.Value ||
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null &&
                    line.SourceInvoiceLine.InvoiceId == invoiceId.Value)
                : query.Where(line =>
                    line.InvoiceId == invoiceId.Value);
        }

        if (filters.FromDate.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.InvoiceDate >=
                            filters.FromDate.Value
                        : line.Invoice.InvoiceDate >=
                            filters.FromDate.Value)
                : query.Where(line =>
                    line.Invoice.InvoiceDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.InvoiceDate <=
                            filters.ToDate.Value
                        : line.Invoice.InvoiceDate <=
                            filters.ToDate.Value)
                : query.Where(line =>
                    line.Invoice.InvoiceDate <= filters.ToDate.Value);
        }

        if (filters.BusinessPartnerId.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.BusinessPartnerId ==
                            filters.BusinessPartnerId.Value
                        : line.Invoice.BusinessPartnerId ==
                            filters.BusinessPartnerId.Value)
                : query.Where(line =>
                    line.Invoice.BusinessPartnerId ==
                    filters.BusinessPartnerId.Value);
        }

        if (filters.StoreId.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.StoreId ==
                            filters.StoreId.Value
                        : line.Invoice.StoreId == filters.StoreId.Value)
                : query.Where(line =>
                    line.Invoice.StoreId == filters.StoreId.Value);
        }

        if (filters.ItemId.HasValue)
        {
            query = query.Where(line =>
                line.ItemId == filters.ItemId.Value);
        }

        if (filters.ItemsCategoryId.HasValue)
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                    line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.ItemsCategoryId ==
                            filters.ItemsCategoryId.Value
                        : line.Invoice.ItemsCategoryId ==
                            filters.ItemsCategoryId.Value)
                : query.Where(line =>
                    line.Invoice.ItemsCategoryId ==
                    filters.ItemsCategoryId.Value);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = alignReturnsToSourceInvoice
                ? query.Where(line =>
                    line.Item!.Code.Contains(search) ||
                    line.Item.Name.Contains(search) ||
                    (line.Invoice.InvoiceType == InvoiceType.SalesReturn &&
                     line.SourceInvoiceLine != null
                        ? line.SourceInvoiceLine.Invoice.InvoiceNumber
                                .Contains(search) ||
                            line.SourceInvoiceLine.Invoice.PartnerInvoiceNo !=
                                null &&
                            line.SourceInvoiceLine.Invoice.PartnerInvoiceNo
                                .Contains(search) ||
                            line.SourceInvoiceLine.Invoice.BusinessPartner.Code
                                .Contains(search) ||
                            line.SourceInvoiceLine.Invoice.BusinessPartner.Name
                                .Contains(search) ||
                            line.SourceInvoiceLine.Invoice.Store.Code
                                .Contains(search) ||
                            line.SourceInvoiceLine.Invoice.Store.Name
                                .Contains(search)
                        : line.Invoice.InvoiceNumber.Contains(search) ||
                            line.Invoice.PartnerInvoiceNo != null &&
                            line.Invoice.PartnerInvoiceNo.Contains(search) ||
                            line.Invoice.BusinessPartner.Code.Contains(search) ||
                            line.Invoice.BusinessPartner.Name.Contains(search) ||
                            line.Invoice.Store.Code.Contains(search) ||
                            line.Invoice.Store.Name.Contains(search)))
                : query.Where(line =>
                    line.Invoice.InvoiceNumber.Contains(search) ||
                    (line.Invoice.PartnerInvoiceNo != null &&
                     line.Invoice.PartnerInvoiceNo.Contains(search)) ||
                    line.Invoice.BusinessPartner.Code.Contains(search) ||
                    line.Invoice.BusinessPartner.Name.Contains(search) ||
                    line.Invoice.Store.Code.Contains(search) ||
                    line.Invoice.Store.Name.Contains(search) ||
                    line.Item!.Code.Contains(search) ||
                    line.Item.Name.Contains(search));
        }

        var movements = dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                (movement.MovementType == ItemMovementType.Sales ||
                 movement.MovementType == ItemMovementType.SalesReturn));
        var projections = await (
                from line in query
                join movement in movements
                    on new
                    {
                        ReferenceId = line.InvoiceId,
                        ItemId = line.ItemId!.Value,
                        MovementType = line.Invoice.InvoiceType ==
                            InvoiceType.Sales
                                ? ItemMovementType.Sales
                                : ItemMovementType.SalesReturn
                    }
                    equals new
                    {
                        movement.ReferenceId,
                        movement.ItemId,
                        movement.MovementType
                    }
                    into matchingMovements
                from movement in matchingMovements.DefaultIfEmpty()
                select new ProfitabilityLineProjection
                {
                    InvoiceLineId = line.Id,
                    InvoiceId = line.InvoiceId,
                    SourceInvoiceId = line.SourceInvoiceLine != null
                    ? line.SourceInvoiceLine.InvoiceId
                    : null,
                    InvoiceNumber = line.Invoice.InvoiceNumber,
                    InvoiceDate = line.Invoice.InvoiceDate,
                    InvoiceType = line.Invoice.InvoiceType,
                    BusinessPartnerId = line.Invoice.BusinessPartnerId,
                    BusinessPartnerName = line.Invoice.BusinessPartner.Name,
                    StoreId = line.Invoice.StoreId,
                    StoreName = line.Invoice.Store.Name,
                    InvoiceBaseSubtotal = line.Invoice.BaseSubtotal,
                    InvoiceBaseDiscountAmount =
                        line.Invoice.BaseDiscountAmount,
                    ItemId = line.ItemId!.Value,
                    ItemCode = line.Item!.Code,
                    ItemName = line.Item.Name,
                    ItemUnitName = line.ItemUnit!.Name,
                    Quantity = line.Quantity,
                    BaseUnitPrice = line.BaseUnitPrice,
                    BaseTotal = line.BaseTotal,
                    CostStatus = movement == null
                        ? null
                        : movement.CostStatus,
                    PendingCostQuantity = movement == null
                        ? null
                        : movement.PendingCostQuantity,
                    UnitCost = movement == null
                        ? null
                        : movement.UnitCost,
                    TotalCost = movement == null
                        ? null
                        : movement.TotalCost
                })
            .ToListAsync(cancellationToken);

        var lines = projections
            .Select(BuildLine)
            .ToArray();
        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        var summary = BuildSummary(lines);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        ReportingMetrics.ProfitabilityDuration.Record(
            elapsed.TotalMilliseconds);
        ReportingMetrics.ProfitabilityLoadedLines.Record(lines.LongLength);
        logger?.LogInformation(
            "Profitability report loaded {LineCount} lines for company {CompanyId} in {ElapsedMilliseconds} ms.",
            lines.Length,
            companyId,
            elapsed.TotalMilliseconds);

        return new ProfitabilityReportData(
            BaseCurrency: baseCurrency,
            Lines: lines,
            Summary: summary);
    }

    private static ProfitabilityLine BuildLine(
        ProfitabilityLineProjection projection)
    {
        var sign = projection.InvoiceType == InvoiceType.Sales
            ? 1m
            : -1m;
        var allocatedDiscount = projection.InvoiceBaseSubtotal == 0m
            ? 0m
            : InventoryCostRules.RoundValue(
                projection.InvoiceBaseDiscountAmount *
                projection.BaseTotal /
                projection.InvoiceBaseSubtotal);
        var grossRevenue = InventoryCostRules.RoundValue(
            sign * projection.BaseTotal);
        var discountAmount = InventoryCostRules.RoundValue(
            sign * allocatedDiscount);
        var netRevenue = InventoryCostRules.RoundValue(
            grossRevenue - discountAmount);
        var recognizedCost = InventoryCostRules.RoundValue(
            sign * projection.TotalCost.GetValueOrDefault());
        var costStatus = projection.CostStatus ??
            InventoryCostStatus.Pending;
        var pendingCostQuantity = projection.PendingCostQuantity ??
            projection.Quantity;
        var isCostFinal = projection.CostStatus.HasValue &&
            (costStatus is InventoryCostStatus.Final or
                InventoryCostStatus.Revalued) &&
            pendingCostQuantity == 0m;
        decimal? grossProfit = isCostFinal
            ? InventoryCostRules.RoundValue(
                netRevenue - recognizedCost)
            : null;

        return new ProfitabilityLine(
            InvoiceLineId: projection.InvoiceLineId,
            InvoiceId: projection.InvoiceId,
            SourceInvoiceId: projection.SourceInvoiceId,
            InvoiceNumber: projection.InvoiceNumber,
            InvoiceDate: projection.InvoiceDate,
            InvoiceType: projection.InvoiceType,
            BusinessPartnerId: projection.BusinessPartnerId,
            BusinessPartnerName: projection.BusinessPartnerName,
            StoreId: projection.StoreId,
            StoreName: projection.StoreName,
            ItemId: projection.ItemId,
            ItemCode: projection.ItemCode,
            ItemName: projection.ItemName,
            ItemUnitName: projection.ItemUnitName,
            Quantity: InventoryCostRules.RoundQuantity(
                sign * projection.Quantity),
            BaseUnitPrice: projection.BaseUnitPrice,
            GrossRevenue: grossRevenue,
            DiscountAmount: discountAmount,
            NetRevenue: netRevenue,
            CostStatus: costStatus,
            PendingCostQuantity: pendingCostQuantity,
            UnitCost: projection.UnitCost,
            RecognizedCost: recognizedCost,
            GrossProfit: grossProfit,
            GrossMarginPercentage: Margin(grossProfit, netRevenue),
            IsCostFinal: isCostFinal);
    }

    private static InvoiceProfitabilityResponse BuildInvoice(
        IReadOnlyCollection<ProfitabilityLine> lines,
        bool includeDetails)
    {
        var first = lines.First();
        var grossRevenue = Sum(lines, line => line.GrossRevenue);
        var discount = Sum(lines, line => line.DiscountAmount);
        var netRevenue = Sum(lines, line => line.NetRevenue);
        var cost = Sum(lines, line => line.RecognizedCost);
        decimal? profit = lines.All(line => line.IsCostFinal)
            ? InventoryCostRules.RoundValue(netRevenue - cost)
            : null;

        return new InvoiceProfitabilityResponse(
            InvoiceId: first.InvoiceId,
            InvoiceNumber: first.InvoiceNumber,
            InvoiceDate: first.InvoiceDate,
            InvoiceType: first.InvoiceType,
            BusinessPartnerId: first.BusinessPartnerId,
            BusinessPartnerName: first.BusinessPartnerName,
            StoreId: first.StoreId,
            StoreName: first.StoreName,
            GrossRevenue: grossRevenue,
            DiscountAmount: discount,
            NetRevenue: netRevenue,
            RecognizedCost: cost,
            GrossProfit: profit,
            GrossMarginPercentage: Margin(profit, netRevenue),
            CostStatus: AggregateStatus(lines),
            PendingCostQuantity: lines.Sum(line =>
                line.PendingCostQuantity),
            LineCount: lines.Count,
            Lines: includeDetails
                ? lines
                    .OrderBy(line => line.InvoiceLineId)
                    .Select(ToResponse)
                    .ToArray()
                : []);
    }

    private static ItemProfitabilityResponse BuildItem(
        IReadOnlyCollection<ProfitabilityLine> lines)
    {
        var first = lines.First();
        var sales = lines
            .Where(line => line.InvoiceType == InvoiceType.Sales)
            .ToArray();
        var returns = lines
            .Where(line => line.InvoiceType == InvoiceType.SalesReturn)
            .ToArray();
        var pending = lines
            .Where(line => !line.IsCostFinal)
            .ToArray();
        var salesQuantity = sales.Sum(line => line.Quantity);
        var returnQuantity = -returns.Sum(line => line.Quantity);
        var salesRevenue = Sum(sales, line => line.NetRevenue);
        var salesCost = Sum(sales, line => line.RecognizedCost);
        var returnRevenue = -Sum(returns, line => line.NetRevenue);
        var returnCost = -Sum(returns, line => line.RecognizedCost);
        var netRevenue = InventoryCostRules.RoundValue(
            salesRevenue - returnRevenue);
        var recognizedCost = InventoryCostRules.RoundValue(
            salesCost - returnCost);
        decimal? grossProfit = pending.Length == 0
            ? InventoryCostRules.RoundValue(
                netRevenue - recognizedCost)
            : null;

        return new ItemProfitabilityResponse(
            ItemId: first.ItemId,
            ItemCode: first.ItemCode,
            ItemName: first.ItemName,
            ItemUnitName: first.ItemUnitName,
            SalesQuantity: InventoryCostRules.RoundQuantity(
                salesQuantity),
            ReturnQuantity: InventoryCostRules.RoundQuantity(
                returnQuantity),
            NetQuantity: InventoryCostRules.RoundQuantity(
                salesQuantity - returnQuantity),
            SalesRevenue: salesRevenue,
            SalesCost: salesCost,
            ReturnRevenue: returnRevenue,
            ReturnCost: returnCost,
            NetRevenue: netRevenue,
            RecognizedCost: recognizedCost,
            GrossProfit: grossProfit,
            GrossMarginPercentage: Margin(grossProfit, netRevenue),
            CostStatus: AggregateStatus(lines),
            PendingCostQuantity: pending.Sum(line =>
                line.PendingCostQuantity),
            InvoiceCount: lines
                .Select(line => line.InvoiceId)
                .Distinct()
                .Count(),
            LineCount: lines.Count,
            PendingLineCount: pending.Length);
    }

    private static ProfitabilityReportSummaryResponse BuildSummary(
        IReadOnlyCollection<ProfitabilityLine> lines)
    {
        var sales = lines
            .Where(line => line.InvoiceType == InvoiceType.Sales)
            .ToArray();
        var returns = lines
            .Where(line => line.InvoiceType == InvoiceType.SalesReturn)
            .ToArray();
        var finalized = lines
            .Where(line => line.IsCostFinal)
            .ToArray();
        var pending = lines
            .Where(line => !line.IsCostFinal)
            .ToArray();
        var salesRevenue = Sum(sales, line => line.NetRevenue);
        var salesCost = Sum(sales, line => line.RecognizedCost);
        var returnRevenue = -Sum(returns, line => line.NetRevenue);
        var returnCost = -Sum(returns, line => line.RecognizedCost);
        var netRevenue = InventoryCostRules.RoundValue(
            salesRevenue - returnRevenue);
        var recognizedCost = InventoryCostRules.RoundValue(
            salesCost - returnCost);
        decimal? grossProfit = pending.Length == 0
            ? InventoryCostRules.RoundValue(
                netRevenue - recognizedCost)
            : null;
        var finalizedRevenue = Sum(
            finalized,
            line => line.NetRevenue);
        var finalizedCost = Sum(
            finalized,
            line => line.RecognizedCost);
        var finalizedProfit = InventoryCostRules.RoundValue(
            finalizedRevenue - finalizedCost);

        return new ProfitabilityReportSummaryResponse(
            SalesRevenue: salesRevenue,
            SalesCost: salesCost,
            ReturnRevenue: returnRevenue,
            ReturnCost: returnCost,
            NetRevenue: netRevenue,
            RecognizedCost: recognizedCost,
            GrossProfit: grossProfit,
            GrossMarginPercentage: Margin(grossProfit, netRevenue),
            FinalizedNetRevenue: finalizedRevenue,
            FinalizedCost: finalizedCost,
            FinalizedGrossProfit: finalizedProfit,
            FinalizedGrossMarginPercentage: Margin(
                finalizedProfit,
                finalizedRevenue),
            PendingRevenue: Sum(pending, line => line.NetRevenue),
            PendingCostQuantity: pending.Sum(line =>
                line.PendingCostQuantity),
            InvoiceCount: lines
                .Select(line => line.InvoiceId)
                .Distinct()
                .Count(),
            ItemCount: lines
                .Select(line => line.ItemId)
                .Distinct()
                .Count(),
            LineCount: lines.Count,
            PendingInvoiceCount: pending
                .Select(line => line.InvoiceId)
                .Distinct()
                .Count(),
            PendingLineCount: pending.Length);
    }

    private static ProfitabilityReportLineResponse ToResponse(
        ProfitabilityLine line) =>
        new(
            InvoiceLineId: line.InvoiceLineId,
            InvoiceId: line.InvoiceId,
            InvoiceNumber: line.InvoiceNumber,
            InvoiceDate: line.InvoiceDate,
            InvoiceType: line.InvoiceType,
            BusinessPartnerId: line.BusinessPartnerId,
            BusinessPartnerName: line.BusinessPartnerName,
            StoreId: line.StoreId,
            StoreName: line.StoreName,
            ItemId: line.ItemId,
            ItemCode: line.ItemCode,
            ItemName: line.ItemName,
            ItemUnitName: line.ItemUnitName,
            Quantity: line.Quantity,
            BaseUnitPrice: line.BaseUnitPrice,
            GrossRevenue: line.GrossRevenue,
            DiscountAmount: line.DiscountAmount,
            NetRevenue: line.NetRevenue,
            CostStatus: line.CostStatus,
            PendingCostQuantity: line.PendingCostQuantity,
            UnitCost: line.UnitCost,
            RecognizedCost: line.RecognizedCost,
            GrossProfit: line.GrossProfit,
            GrossMarginPercentage: line.GrossMarginPercentage);

    private static InvoiceProfitabilityListItemResponse ToListItemResponse(
        InvoiceProfitabilityResponse invoice) =>
        new(
            InvoiceId: invoice.InvoiceId,
            InvoiceNumber: invoice.InvoiceNumber,
            InvoiceDate: invoice.InvoiceDate,
            InvoiceType: invoice.InvoiceType,
            BusinessPartnerId: invoice.BusinessPartnerId,
            BusinessPartnerName: invoice.BusinessPartnerName,
            StoreId: invoice.StoreId,
            StoreName: invoice.StoreName,
            GrossRevenue: invoice.GrossRevenue,
            DiscountAmount: invoice.DiscountAmount,
            NetRevenue: invoice.NetRevenue,
            RecognizedCost: invoice.RecognizedCost,
            GrossProfit: invoice.GrossProfit,
            GrossMarginPercentage: invoice.GrossMarginPercentage,
            CostStatus: invoice.CostStatus,
            PendingCostQuantity: invoice.PendingCostQuantity,
            LineCount: invoice.LineCount);

    private static InventoryCostStatus AggregateStatus(
        IReadOnlyCollection<ProfitabilityLine> lines)
    {
        var pending = lines
            .Where(line => !line.IsCostFinal)
            .ToArray();
        if (pending.Length > 0)
        {
            return pending.Length == lines.Count &&
                pending.All(line => line.RecognizedCost == 0m)
                    ? InventoryCostStatus.Pending
                    : InventoryCostStatus.PartiallyCosted;
        }

        return lines.Any(line =>
            line.CostStatus == InventoryCostStatus.Revalued)
                ? InventoryCostStatus.Revalued
                : InventoryCostStatus.Final;
    }

    private static decimal Sum(
        IEnumerable<ProfitabilityLine> lines,
        Func<ProfitabilityLine, decimal> selector) =>
        InventoryCostRules.RoundValue(lines.Sum(selector));

    private static decimal? Margin(
        decimal? profit,
        decimal revenue) =>
        !profit.HasValue || revenue == 0m
            ? null
            : decimal.Round(
                profit.Value / revenue * 100m,
                4,
                MidpointRounding.AwayFromZero);

    private static IReadOnlyList<T> Paginate<T>(
        IReadOnlyList<T> items,
        PaginationRequest pagination)
    {
        var offset = (long)(pagination.PageNumber - 1) *
            pagination.PageSize;
        return offset >= items.Count
            ? []
            : items
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToArray();
    }

    private static int TotalPages(int totalCount, int pageSize) =>
        (int)Math.Ceiling(totalCount / (double)pageSize);

    private static Error? Validate(
        PaginationRequest pagination,
        ProfitabilityReportFilterRequest filters)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return PaginationErrors.Invalid();
        }

        if (filters.BusinessPartnerId is <= 0)
        {
            return InvalidFilter(
                nameof(filters.BusinessPartnerId),
                "رقم العميل غير صحيح.");
        }

        if (filters.StoreId is <= 0)
        {
            return InvalidFilter(
                nameof(filters.StoreId),
                "رقم المخزن غير صحيح.");
        }

        if (filters.ItemId is <= 0)
        {
            return InvalidFilter(
                nameof(filters.ItemId),
                "رقم الصنف غير صحيح.");
        }

        if (filters.ItemsCategoryId is <= 0)
        {
            return InvalidFilter(
                nameof(filters.ItemsCategoryId),
                "رقم تصنيف الأصناف غير صحيح.");
        }

        if (filters.FromDate > filters.ToDate)
        {
            return InvalidFilter(
                nameof(filters.ToDate),
                "تاريخ النهاية يجب أن يساوي تاريخ البداية أو يأتي بعده.");
        }

        if (filters.Search?.Trim().Length > 200)
        {
            return InvalidFilter(
                nameof(filters.Search),
                "نص البحث طويل. الحد الأقصى 200 حرف.");
        }

        return null;
    }

    private sealed class ProfitabilityLineProjection
    {
        public int InvoiceLineId { get; init; }
        public int InvoiceId { get; init; }
        public int? SourceInvoiceId { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public DateOnly InvoiceDate { get; init; }
        public InvoiceType InvoiceType { get; init; }
        public int BusinessPartnerId { get; init; }
        public string BusinessPartnerName { get; init; } = string.Empty;
        public int StoreId { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public decimal InvoiceBaseSubtotal { get; init; }
        public decimal InvoiceBaseDiscountAmount { get; init; }
        public int ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string ItemUnitName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal BaseUnitPrice { get; init; }
        public decimal BaseTotal { get; init; }
        public InventoryCostStatus? CostStatus { get; init; }
        public decimal? PendingCostQuantity { get; init; }
        public decimal? UnitCost { get; init; }
        public decimal? TotalCost { get; init; }
    }

    private sealed record ProfitabilityLine(
        int InvoiceLineId,
        int InvoiceId,
        int? SourceInvoiceId,
        string InvoiceNumber,
        DateOnly InvoiceDate,
        InvoiceType InvoiceType,
        int BusinessPartnerId,
        string BusinessPartnerName,
        int StoreId,
        string StoreName,
        int ItemId,
        string ItemCode,
        string ItemName,
        string ItemUnitName,
        decimal Quantity,
        decimal BaseUnitPrice,
        decimal GrossRevenue,
        decimal DiscountAmount,
        decimal NetRevenue,
        InventoryCostStatus CostStatus,
        decimal PendingCostQuantity,
        decimal? UnitCost,
        decimal RecognizedCost,
        decimal? GrossProfit,
        decimal? GrossMarginPercentage,
        bool IsCostFinal);

    private sealed record ProfitabilityReportData(
        CurrencyCode BaseCurrency,
        IReadOnlyList<ProfitabilityLine> Lines,
        ProfitabilityReportSummaryResponse Summary);
}
