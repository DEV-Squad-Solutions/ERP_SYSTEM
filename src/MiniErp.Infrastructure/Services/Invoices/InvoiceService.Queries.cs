using Mapster;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
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
                  invoice.Country.ArabicName.Contains(search))) ||
                (invoice.ItemsCategory != null &&
                 invoice.ItemsCategory.Name.Contains(search)) ||
                (invoice.Driver != null &&
                 (invoice.Driver.Code.Contains(search) ||
                  invoice.Driver.Name.Contains(search))) ||
                (invoice.ActualDriver != null &&
                 (invoice.ActualDriver.Code.Contains(search) ||
                  invoice.ActualDriver.Name.Contains(search))) ||
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

        return totals is null
            ? (0, new InvoiceSummaryResponse(
                Subtotal: 0m,
                DiscountAmount: 0m,
                Total: 0m,
                PaidAmount: 0m,
                RemainingAmount: 0m))
            : (
                totals.TotalCount,
                new InvoiceSummaryResponse(
                    Subtotal: totals.Subtotal,
                    DiscountAmount: totals.DiscountAmount,
                    Total: totals.Total,
                    PaidAmount: totals.PaidAmount,
                    RemainingAmount: totals.RemainingAmount));
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

    private IQueryable<InvoiceResponse> ProjectResponseQuery(int id) =>
        dbContext.Invoices
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.Id == id)
            .ProjectToType<InvoiceResponse>();

    private async Task<InvoiceResponse?> GetResponseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var response = await ProjectResponseQuery(id)
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

    private async Task<Invoice?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .AsSplitQuery()
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .FirstOrDefaultAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.Id == id,
                cancellationToken);

    private Task<bool> HasActiveLinkedReturnsAsync(
        IReadOnlyCollection<int> sourceLineIds,
        CancellationToken cancellationToken) =>
        sourceLineIds.Count > 0
            ? dbContext.InvoiceLines.AnyAsync(
                line =>
                    line.CompanyId == companyId &&
                    line.SourceInvoiceLineId.HasValue &&
                    sourceLineIds.Contains(
                        line.SourceInvoiceLineId.Value) &&
                    (line.Invoice.InvoiceType == InvoiceType.SalesReturn ||
                     line.Invoice.InvoiceType == InvoiceType.PurchaseReturn),
                cancellationToken)
            : Task.FromResult(false);

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
