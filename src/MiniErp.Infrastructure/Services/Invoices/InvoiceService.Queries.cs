using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;

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
                    line.Item.Code.Contains(search) ||
                    line.Item.Name.Contains(search)) ||
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
            ? (0, new InvoiceSummaryResponse(0m, 0m, 0m, 0m, 0m))
            : (
                totals.TotalCount,
                new InvoiceSummaryResponse(
                    totals.Subtotal,
                    totals.DiscountAmount,
                    totals.Total,
                    totals.PaidAmount,
                    totals.RemainingAmount));
    }

    public async Task<Result<InvoiceItemBalanceResponse>> GetItemBalanceAsync(
        int storeId,
        int itemId,
        DateOnly asOfDate,
        int? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Validation(
                    "Invoices.ItemBalanceStoreInvalid",
                    "يجب أن يكون رقم المخزن أكبر من صفر.",
                    nameof(InvoiceItemBalanceResponse.StoreId)));
        }

        if (itemId <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Validation(
                    "Invoices.ItemBalanceItemInvalid",
                    "يجب أن يكون رقم الصنف أكبر من صفر.",
                    nameof(InvoiceItemBalanceResponse.ItemId)));
        }

        if (asOfDate == DateOnly.MinValue)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Validation(
                    "Invoices.ItemBalanceDateRequired",
                    "يجب تحديد تاريخ الفاتورة لحساب الرصيد.",
                    nameof(InvoiceItemBalanceResponse.AsOfDate)));
        }

        if (invoiceId is <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Validation(
                    "Invoices.ItemBalanceInvoiceInvalid",
                    "يجب أن يكون رقم الفاتورة المستبعدة أكبر من صفر.",
                    "InvoiceId"));
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == storeId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.NotFound(
                    "Invoices.StoreNotFound",
                    "لم يتم العثور على المخزن المحدد.",
                    nameof(InvoiceItemBalanceResponse.StoreId)));
        }

        if (!store.IsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Conflict(
                    "Invoices.StoreInactive",
                    "لا يمكن حساب رصيد مخزن غير نشط.",
                    nameof(InvoiceItemBalanceResponse.StoreId)));
        }

        if (store.IsContainerStore)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Conflict(
                    "Invoices.ContainerStoreNotAllowed",
                    "رصيد الأصناف متاح لمخازن المنتجات فقط.",
                    nameof(InvoiceItemBalanceResponse.StoreId)));
        }

        var item = await dbContext.Items
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == itemId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.ItemUnitId,
                ItemUnitName = candidate.ItemUnit.Name,
                candidate.IsActive,
                ItemUnitIsActive = candidate.ItemUnit.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.NotFound(
                    "Invoices.ItemNotFound",
                    "لم يتم العثور على الصنف المحدد.",
                    nameof(InvoiceItemBalanceResponse.ItemId)));
        }

        if (!item.IsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Conflict(
                    "Invoices.ItemInactive",
                    "لا يمكن حساب رصيد صنف غير نشط.",
                    nameof(InvoiceItemBalanceResponse.ItemId)));
        }

        if (!item.ItemUnitIsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                Error.Conflict(
                    "Invoices.ItemUnitInactive",
                    "لا يمكن حساب رصيد صنف وحدته غير نشطة.",
                    nameof(InvoiceItemBalanceResponse.ItemId)));
        }

        string? excludedInvoiceNumber = null;
        if (invoiceId is int currentInvoiceId)
        {
            excludedInvoiceNumber = await dbContext.Invoices
                .AsNoTracking()
                .Where(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == currentInvoiceId)
                .Select(candidate => candidate.InvoiceNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (excludedInvoiceNumber is null)
            {
                return Result<InvoiceItemBalanceResponse>.Failure(
                    NotFound(currentInvoiceId));
            }
        }

        var excludedMovement = invoiceId is int excludedInvoiceId
            ? new InventoryMovementReference(
                InvoiceItemMovementTypes,
                excludedInvoiceId,
                excludedInvoiceNumber!)
            : null;
        var balances = await inventoryStockService.GetBalancesAsync(
            storeId,
            [itemId],
            asOfDate,
            excludedMovement,
            cancellationToken);

        return Result<InvoiceItemBalanceResponse>.Success(
            new InvoiceItemBalanceResponse(
                storeId,
                store.Name,
                itemId,
                item.Name,
                item.ItemUnitId,
                item.ItemUnitName,
                asOfDate,
                balances[itemId]));
    }

    private IQueryable<InvoiceResponse> ProjectResponseQuery(int id) =>
        dbContext.Invoices
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.Id == id)
            .ProjectToType<InvoiceResponse>();

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
}
