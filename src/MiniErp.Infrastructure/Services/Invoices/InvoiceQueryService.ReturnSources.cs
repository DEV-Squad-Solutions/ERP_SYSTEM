using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceQueryService
{
    public async Task<Result<PagedResponse<InvoiceReturnSourceResponse>>>
        GetReturnSourcesAsync(
            PaginationRequest pagination,
            InvoiceReturnSourceFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<InvoiceReturnSourceResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        var filterError = ValidateReturnSourceFilters(filters);
        if (filterError is not null)
        {
            return Result<PagedResponse<InvoiceReturnSourceResponse>>.Failure(
                filterError);
        }

        var sourceType = filters.ReturnType == InvoiceReturnType.SalesReturn
            ? InvoiceType.Sales
            : InvoiceType.Purchase;
        var sourceMovementType =
            filters.ReturnType == InvoiceReturnType.SalesReturn
                ? ItemMovementType.Sales
                : ItemMovementType.Purchase;
        var returnType = filters.ReturnType == InvoiceReturnType.SalesReturn
            ? InvoiceType.SalesReturn
            : InvoiceType.PurchaseReturn;
        var currentReturnInvoiceId = filters.CurrentReturnInvoiceId;

        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.InvoiceType == sourceType &&
                invoice.ContentType == InvoiceContentType.Items &&
                invoice.BusinessPartnerId == filters.BusinessPartnerId &&
                invoice.StoreId == filters.StoreId &&
                invoice.InvoiceDate <= filters.AsOfDate);

        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(search) ||
                (invoice.PartnerInvoiceNo != null &&
                 invoice.PartnerInvoiceNo.Contains(search)));
        }

        query = query.Where(invoice => invoice.Lines.Any(sourceLine =>
            sourceLine.ItemId.HasValue &&
            sourceLine.ItemUnitId.HasValue &&
            sourceLine.Quantity -
            (dbContext.InvoiceLines
                .Where(returnLine =>
                    returnLine.CompanyId == companyId &&
                    returnLine.SourceInvoiceLineId == sourceLine.Id &&
                    returnLine.Invoice.InvoiceType == returnType &&
                    (!currentReturnInvoiceId.HasValue ||
                     returnLine.InvoiceId != currentReturnInvoiceId.Value))
                .Sum(returnLine => (decimal?)returnLine.Quantity) ?? 0m) > 0m));

        var orderedQuery = query
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenByDescending(invoice => invoice.Id);
        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;

        var invoices = offset >= totalCount
            ? []
            : await orderedQuery
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(invoice => new
                {
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.PartnerInvoiceNo,
                    invoice.InvoiceDate,
                    invoice.InvoiceType,
                    invoice.BusinessPartnerId,
                    BusinessPartnerName = invoice.BusinessPartner.Name,
                    invoice.StoreId,
                    StoreName = invoice.Store.Name,
                    invoice.Currency,
                    OriginalSubtotal = invoice.Lines.Sum(line => line.Total),
                    OriginalDiscountAmount = invoice.DiscountAmount,
                    OriginalTotal = invoice.Total
                })
                .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(invoice => invoice.Id).ToArray();
        var sourceLines = invoiceIds.Length == 0
            ? []
            : await dbContext.InvoiceLines
                .AsNoTracking()
                .Where(sourceLine =>
                    sourceLine.CompanyId == companyId &&
                    invoiceIds.Contains(sourceLine.InvoiceId) &&
                    sourceLine.ItemId.HasValue &&
                    sourceLine.ItemUnitId.HasValue)
                .Select(sourceLine => new
                {
                    sourceLine.InvoiceId,
                    SourceInvoiceLineId = sourceLine.Id,
                    ItemId = sourceLine.ItemId!.Value,
                    ItemCode = sourceLine.Item!.Code,
                    ItemName = sourceLine.Item.Name,
                    ItemUnitId = sourceLine.ItemUnitId!.Value,
                    ItemUnitName = sourceLine.ItemUnit!.Name,
                    sourceLine.Count,
                    sourceLine.Weight,
                    OriginalQuantity = sourceLine.Quantity,
                    ReturnedQuantity = dbContext.InvoiceLines
                        .Where(returnLine =>
                            returnLine.CompanyId == companyId &&
                            returnLine.SourceInvoiceLineId == sourceLine.Id &&
                            returnLine.Invoice.InvoiceType == returnType &&
                            (!currentReturnInvoiceId.HasValue ||
                             returnLine.InvoiceId !=
                             currentReturnInvoiceId.Value))
                        .Sum(returnLine =>
                            (decimal?)returnLine.Quantity) ?? 0m,
                    UnitPrice = sourceLine.Price,
                    OriginalTotal = sourceLine.Total,
                    CostStatus = dbContext.ItemMovements
                        .Where(movement =>
                            movement.CompanyId == companyId &&
                            movement.MovementType == sourceMovementType &&
                            movement.ReferenceId == sourceLine.InvoiceId &&
                            movement.ItemId == sourceLine.ItemId)
                        .Select(movement =>
                            (InventoryCostStatus?)movement.CostStatus)
                        .SingleOrDefault(),
                    PendingCostQuantity = dbContext.ItemMovements
                        .Where(movement =>
                            movement.CompanyId == companyId &&
                            movement.MovementType == sourceMovementType &&
                            movement.ReferenceId == sourceLine.InvoiceId &&
                            movement.ItemId == sourceLine.ItemId)
                        .Select(movement =>
                            (decimal?)movement.PendingCostQuantity)
                        .SingleOrDefault(),
                    UnitCost = dbContext.ItemMovements
                        .Where(movement =>
                            movement.CompanyId == companyId &&
                            movement.MovementType == sourceMovementType &&
                            movement.ReferenceId == sourceLine.InvoiceId &&
                            movement.ItemId == sourceLine.ItemId)
                        .Select(movement => movement.UnitCost)
                        .SingleOrDefault()
                })
                .OrderBy(sourceLine => sourceLine.InvoiceId)
                .ThenBy(sourceLine => sourceLine.SourceInvoiceLineId)
                .ToListAsync(cancellationToken);

        var linesByInvoice = sourceLines
            .GroupBy(line => line.InvoiceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<InvoiceReturnSourceLineResponse>)group
                    .Select(line => new InvoiceReturnSourceLineResponse(
                        SourceInvoiceLineId: line.SourceInvoiceLineId,
                        ItemId: line.ItemId,
                        ItemCode: line.ItemCode,
                        ItemName: line.ItemName,
                        ItemUnitId: line.ItemUnitId,
                        ItemUnitName: line.ItemUnitName,
                        Count: line.Count,
                        Weight: line.Weight,
                        OriginalQuantity: line.OriginalQuantity,
                        ReturnedQuantity: line.ReturnedQuantity,
                        AvailableQuantity:
                            line.OriginalQuantity - line.ReturnedQuantity,
                        UnitPrice: line.UnitPrice,
                        OriginalTotal: line.OriginalTotal,
                        CostStatus:
                            line.CostStatus ?? InventoryCostStatus.Pending,
                        PendingCostQuantity:
                            line.PendingCostQuantity ?? line.OriginalQuantity,
                        UnitCost: line.UnitCost))
                    .ToArray());

        var items = invoices
            .Select(invoice => new InvoiceReturnSourceResponse(
                InvoiceId: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                PartnerInvoiceNo: invoice.PartnerInvoiceNo,
                InvoiceDate: invoice.InvoiceDate,
                InvoiceType: invoice.InvoiceType,
                BusinessPartnerId: invoice.BusinessPartnerId,
                BusinessPartnerName: invoice.BusinessPartnerName,
                StoreId: invoice.StoreId,
                StoreName: invoice.StoreName,
                Currency: invoice.Currency,
                OriginalSubtotal: invoice.OriginalSubtotal,
                OriginalDiscountAmount: invoice.OriginalDiscountAmount,
                OriginalTotal: invoice.OriginalTotal,
                Lines: linesByInvoice.GetValueOrDefault(invoice.Id, [])))
            .ToArray();
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<PagedResponse<InvoiceReturnSourceResponse>>.Success(
            new PagedResponse<InvoiceReturnSourceResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    private static Error? ValidateReturnSourceFilters(
        InvoiceReturnSourceFilterRequest filters)
    {
        if (filters.BusinessPartnerId <= 0)
        {
            return ReturnSourcePartnerInvalid();
        }

        if (filters.StoreId <= 0)
        {
            return ReturnSourceStoreInvalid();
        }

        if (filters.ReturnType is not
            (InvoiceReturnType.SalesReturn or
             InvoiceReturnType.PurchaseReturn))
        {
            return ReturnSourceTypeInvalid();
        }

        if (filters.AsOfDate == DateOnly.MinValue)
        {
            return ReturnSourceDateRequired();
        }

        if (filters.Search?.Trim().Length >
            InvoiceRequest.InvoiceNumberMaximumLength)
        {
            return ReturnSourceSearchInvalid();
        }

        return filters.CurrentReturnInvoiceId is <= 0
            ? CurrentReturnInvoiceInvalid()
            : null;
    }
}
