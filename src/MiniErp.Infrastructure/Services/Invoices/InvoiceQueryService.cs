using Mapster;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceQueryService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInvoiceInventoryService invoiceInventoryService,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver)
    : IInvoiceQueryService, IScopedService
{
    private static readonly ItemMovementType[] InvoiceItemMovementTypes =
    [
        ItemMovementType.Sales,
        ItemMovementType.SalesReturn,
        ItemMovementType.Purchase,
        ItemMovementType.PurchaseReturn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InvoicePagedResponse>> GetAllAsync(
        PaginationRequest pagination,
        InvoiceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new InvoiceFilterRequest();
        var filterError = ValidateFilters(filters);
        if (filterError is not null)
        {
            return Result<InvoicePagedResponse>.Failure(filterError);
        }

        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<InvoicePagedResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYearId = fiscalYearResult.Value.FiscalYearId;

        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.FiscalYearId == fiscalYearId);

        query = ApplyFilters(query, filters);

        var orderedQuery = query
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenByDescending(invoice => invoice.Id);

        var aggregate = await GetSummaryAsync(
            query,
            filters.ItemId,
            cancellationToken);
        var pageResult = await paginationService.PaginateAsync<
            Invoice,
            InvoiceListResponse>(
            orderedQuery,
            pagination,
            aggregate.TotalCount,
            cancellationToken);

        if (pageResult.IsFailure)
        {
            return Result<InvoicePagedResponse>.Failure(pageResult.Error);
        }

        var page = pageResult.Value;

        return Result<InvoicePagedResponse>.Success(
            new InvoicePagedResponse(
                Items: page.Items,
                PageNumber: page.PageNumber,
                PageSize: page.PageSize,
                TotalCount: page.TotalCount,
                TotalPages: page.TotalPages,
                Summary: aggregate.Summary));
    }

    public async Task<Result<InvoiceResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InvoiceResponse>.Failure(InvalidId());
        }

        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(fiscalYearResult.Errors);
        }

        var response = await GetResponseAsync(
            id,
            fiscalYearResult.Value.FiscalYearId,
            cancellationToken);

        return response is null
            ? Result<InvoiceResponse>.Failure(NotFound(id))
            : Result<InvoiceResponse>.Success(response);
    }

    private static Error? ValidateFilters(InvoiceFilterRequest filters)
    {
        if (filters.InvoiceNumber?.Trim().Length >
            InvoiceRequest.InvoiceNumberMaximumLength)
        {
            return InvoiceNumberFilterInvalid();
        }

        if (filters.InvoiceType.HasValue &&
            !Enum.IsDefined(
                typeof(InvoiceType),
                filters.InvoiceType.Value))
        {
            return InvoiceTypeInvalid(nameof(InvoiceFilterRequest.InvoiceType));
        }

        if (filters.PaymentTerm.HasValue &&
            !Enum.IsDefined(
                typeof(PaymentTerm),
                filters.PaymentTerm.Value))
        {
            return PaymentTermInvalid(nameof(InvoiceFilterRequest.PaymentTerm));
        }

        if (filters.PriceStatus.HasValue &&
            !Enum.IsDefined(
                typeof(InvoicePriceStatus),
                filters.PriceStatus.Value))
        {
            return InvalidFilter(InvoiceFilterErrorKind.PriceStatus);
        }

        if (filters.BusinessPartnerId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.BusinessPartnerId);
        }

        if (filters.CountryId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.CountryId);
        }

        if (filters.StoreId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.StoreId);
        }

        if (filters.DriverId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.DriverId);
        }

        if (filters.ItemId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.ItemId);
        }

        if (filters.FiscalYearId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.FiscalYearId);
        }

        if (filters.FromDate > filters.ToDate)
        {
            return InvalidFilter(InvoiceFilterErrorKind.DateRange);
        }

        return null;
    }

    private static IQueryable<Invoice> ApplyFilters(
        IQueryable<Invoice> query,
        InvoiceFilterRequest filters)
    {
        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(search) ||
                (invoice.ExportInvoiceCode != null &&
                 invoice.ExportInvoiceCode.Contains(search)) ||
                invoice.BusinessPartner.Code.Contains(search) ||
                invoice.BusinessPartner.Name.Contains(search) ||
                invoice.Store.Code.Contains(search) ||
                invoice.Store.Name.Contains(search) ||
                (invoice.ContainerStore != null &&
                 (invoice.ContainerStore.Code.Contains(search) ||
                  invoice.ContainerStore.Name.Contains(search))) ||
                (invoice.Country != null &&
                 (invoice.Country.Code.Contains(search) ||
                  invoice.Country.Name.Contains(search) ||
                  invoice.Country.EnglishName.Contains(search))) ||
                (invoice.ItemsCategory != null &&
                 invoice.ItemsCategory.Name.Contains(search)) ||
                (invoice.Driver != null &&
                 (invoice.Driver.Code.Contains(search) ||
                  invoice.Driver.Name.Contains(search))) ||
                (invoice.ActualDriverName != null &&
                 invoice.ActualDriverName.Contains(search)) ||
                (invoice.ExternalDriverName != null &&
                 invoice.ExternalDriverName.Contains(search)) ||
                (invoice.VehicleNumber != null &&
                 invoice.VehicleNumber.Contains(search)) ||
                invoice.Lines.Any(line =>
                    line.Item != null &&
                    (line.Item.Code.Contains(search) ||
                    line.Item.Name.Contains(search))) ||
                invoice.ContainerLines.Any(line =>
                    line.Container.Code.Contains(search) ||
                    line.Container.Name.Contains(search)));
        }

        var invoiceNumber = filters.InvoiceNumber?.Trim();
        if (!string.IsNullOrEmpty(invoiceNumber))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(invoiceNumber));
        }

        if (filters.InvoiceType.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceType == filters.InvoiceType.Value);
        }

        if (filters.BusinessPartnerId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.BusinessPartnerId ==
                filters.BusinessPartnerId.Value);
        }

        if (filters.CountryId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.CountryId == filters.CountryId.Value);
        }

        if (filters.StoreId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.StoreId == filters.StoreId.Value);
        }

        if (filters.DriverId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.DriverId == filters.DriverId.Value);
        }

        if (filters.ItemId.HasValue)
        {
            query = query.Where(invoice =>
                invoice.Lines.Any(line =>
                    line.ItemId == filters.ItemId.Value));
        }

        if (filters.PaymentTerm.HasValue)
        {
            query = query.Where(invoice =>
                invoice.PaymentTerm == filters.PaymentTerm.Value);
        }

        if (filters.PriceStatus == InvoicePriceStatus.HasMissingPrice)
        {
            query = query.Where(invoice =>
                invoice.Lines.Any(line => line.Price == 0m));
        }
        else if (filters.PriceStatus == InvoicePriceStatus.AllItemsPriced)
        {
            query = query.Where(invoice =>
                invoice.Lines.Any() &&
                invoice.Lines.All(line => line.Price > 0m));
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(invoice =>
                invoice.InvoiceDate <= filters.ToDate.Value);
        }

        return query;
    }

    private static async Task<(int TotalCount, InvoiceSummaryResponse Summary)>
        GetSummaryAsync(
            IQueryable<Invoice> query,
            int? itemId,
            CancellationToken cancellationToken)
    {
        var totals = await query
            .GroupBy(_ => 1)
            .Select(invoices => new
            {
                TotalCount = invoices.Count(),
                Subtotal = invoices.Sum(invoice =>
                    invoice.Total + invoice.DiscountAmount),
                DiscountAmount = invoices.Sum(invoice =>
                    invoice.DiscountAmount),
                Total = invoices.Sum(invoice => invoice.Total),
                PaidAmount = invoices.Sum(invoice => invoice.PaidAmount),
                RemainingAmount = invoices.Sum(invoice =>
                    invoice.Total - invoice.PaidAmount)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var quantityQuery = query.SelectMany(invoice => invoice.Lines);
        if (itemId.HasValue)
        {
            quantityQuery = quantityQuery.Where(line =>
                line.ItemId == itemId.Value);
        }

        var totalQuantity = await quantityQuery
            .SumAsync(
                line => (decimal?)line.Quantity,
                cancellationToken) ?? 0m;

        return totals is null
            ? (0, new InvoiceSummaryResponse(
                Subtotal: 0m,
                DiscountAmount: 0m,
                Total: 0m,
                PaidAmount: 0m,
                RemainingAmount: 0m,
                TotalQuantity: totalQuantity))
            : (
                totals.TotalCount,
                new InvoiceSummaryResponse(
                    Subtotal: totals.Subtotal,
                    DiscountAmount: totals.DiscountAmount,
                    Total: totals.Total,
                    PaidAmount: totals.PaidAmount,
                    RemainingAmount: totals.RemainingAmount,
                    TotalQuantity: totalQuantity));
    }

    public async Task<Result<InvoiceItemBalanceResponse>> GetItemBalanceAsync(
        int storeId,
        int itemId,
        DateOnly asOfDate,
        int? invoiceId = null,
        CancellationToken cancellationToken = default) =>
        await invoiceInventoryService.GetItemBalanceAsync(
            storeId,
            itemId,
            asOfDate,
            invoiceId,
            cancellationToken);

    private IQueryable<InvoiceResponse> ProjectResponseQuery(
        int id,
        int fiscalYearId) =>
        dbContext.Invoices
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.FiscalYearId == fiscalYearId &&
                invoice.Id == id)
            .ProjectToType<InvoiceResponse>();

    private async Task<InvoiceResponse?> GetResponseAsync(
        int id,
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var response = await ProjectResponseQuery(id, fiscalYearId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (response is null)
        {
            return null;
        }

        var movementTypes = InvoiceItemMovementTypes;
        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYearId &&
                movementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == id)
            .Select(movement => new InvoiceLineCostSnapshot(
                movement.ItemId,
                movement.CostStatus,
                movement.PendingCostQuantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.QuantityAfter,
                movement.AverageCostAfter,
                movement.InventoryValueAfter))
            .ToDictionaryAsync(
                movement => movement.ItemId,
                cancellationToken);

        return response with
        {
            Lines = response.Lines
                .Select(line =>
                {
                    if (!line.ItemId.HasValue ||
                        !movements.TryGetValue(
                            line.ItemId.Value,
                            out var movement))
                    {
                        return line;
                    }

                    return line with
                    {
                        CostStatus = movement.CostStatus,
                        PendingCostQuantity =
                            movement.PendingCostQuantity,
                        UnitCost = movement.UnitCost,
                        InventoryTotalCost = movement.TotalCost,
                        QuantityAfter = movement.QuantityAfter,
                        AverageCostAfter = movement.AverageCostAfter,
                        InventoryValueAfter =
                            movement.InventoryValueAfter
                    };
                })
                .ToArray()
        };
    }

    private sealed record InvoiceLineCostSnapshot(
        int ItemId,
        InventoryCostStatus CostStatus,
        decimal PendingCostQuantity,
        decimal? UnitCost,
        decimal TotalCost,
        decimal QuantityAfter,
        decimal AverageCostAfter,
        decimal InventoryValueAfter);
}
