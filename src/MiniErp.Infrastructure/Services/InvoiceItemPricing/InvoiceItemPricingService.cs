using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.InvoiceItemPricing;
using MiniErp.Application.Features.Items;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.InvoiceItemPricing.InvoiceItemPricingErrors;

namespace MiniErp.Infrastructure.Services.InvoiceItemPricing;

public sealed class InvoiceItemPricingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IInvoiceItemPricingService, IScopedService
{
    private static readonly ItemMovementType[] InvoiceMovementTypes =
    [
        ItemMovementType.Sales,
        ItemMovementType.SalesReturn,
        ItemMovementType.Purchase,
        ItemMovementType.PurchaseReturn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InvoiceItemPricingPagedResponse>> GetAsync(
        PaginationRequest pagination,
        InvoiceItemPricingFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(pagination, filters);
        if (validationError is not null)
        {
            return Result<InvoiceItemPricingPagedResponse>.Failure(
                validationError);
        }

        var query = ApplyFilters(CreateLineQuery(), filters);
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var pageLines = offset >= totalCount
            ? []
            : await ProjectLines(query)
                .OrderByDescending(line => line.InvoiceDate)
                .ThenByDescending(line => line.InvoiceId)
                .ThenBy(line => line.ItemName)
                .ThenBy(line => line.InvoiceLineId)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var items = await BuildRowsAsync(pageLines, cancellationToken);
        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);

        return Result<InvoiceItemPricingPagedResponse>.Success(
            new InvoiceItemPricingPagedResponse(
                BaseCurrency: baseCurrency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: (int)Math.Ceiling(
                    totalCount / (double)pagination.PageSize)));
    }

    public async Task<Result<ItemPricingExpensesResponse>> ReplaceExpensesAsync(
        int itemId,
        ReplaceItemPricingExpensesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return Result<ItemPricingExpensesResponse>.Failure(
                InvalidItemId());
        }

        var requestedExpenses = request.Expenses ?? [];
        if (requestedExpenses.Count > 25)
        {
            return Result<ItemPricingExpensesResponse>.Failure(
                InvalidFilters(
                    "لا يمكن إضافة أكثر من 25 مصروفًا استرشاديًا للصنف.",
                    nameof(request.Expenses)));
        }

        var duplicateNames = requestedExpenses
            .Select(expense => expense.Name?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != requestedExpenses.Count;
        if (duplicateNames)
        {
            return Result<ItemPricingExpensesResponse>.Failure(
                InvalidFilters(
                    "لا يمكن تكرار اسم المصروف داخل الصنف.",
                    nameof(request.Expenses)));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var itemExists = await dbContext.Items
            .AnyAsync(item =>
                item.CompanyId == companyId && item.Id == itemId,
                cancellationToken);
        if (!itemExists)
        {
            return Result<ItemPricingExpensesResponse>.Failure(
                ItemNotFound(itemId));
        }

        var currentExpenses = await dbContext.ItemPricingExpenses
            .Where(expense =>
                expense.CompanyId == companyId &&
                expense.ItemId == itemId)
            .ToListAsync(cancellationToken);

        if (currentExpenses.Count > 0)
        {
            dbContext.ItemPricingExpenses.RemoveRange(currentExpenses);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var newExpenses = requestedExpenses
            .Select(expense => new ItemPricingExpense
            {
                CompanyId = companyId,
                ItemId = itemId,
                Name = expense.Name.Trim(),
                Amount = InventoryCostRules.RoundValue(expense.Amount),
                Notes = string.IsNullOrWhiteSpace(expense.Notes)
                    ? null
                    : expense.Notes.Trim()
            })
            .ToArray();

        if (newExpenses.Length > 0)
        {
            dbContext.ItemPricingExpenses.AddRange(newExpenses);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var expenses = await LoadItemExpensesAsync([itemId], cancellationToken);
        var responseExpenses = expenses.GetValueOrDefault(itemId, []);
        return Result<ItemPricingExpensesResponse>.Success(
            new ItemPricingExpensesResponse(
                ItemId: itemId,
                ExpensesPerUnit: InventoryCostRules.RoundValue(
                    responseExpenses.Sum(expense => expense.Amount)),
                Expenses: responseExpenses));
    }

    private IQueryable<InvoiceLine> CreateLineQuery() =>
        dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.Invoice.CompanyId == companyId &&
                line.ItemId.HasValue);

    private static IQueryable<InvoiceLine> ApplyFilters(
        IQueryable<InvoiceLine> query,
        InvoiceItemPricingFilterRequest filters)
    {
        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(line =>
                line.Invoice.InvoiceNumber.Contains(search) ||
                line.Invoice.BusinessPartner.Name.Contains(search) ||
                line.Invoice.Store.Name.Contains(search) ||
                (line.Item != null &&
                 (line.Item.Code.Contains(search) ||
                  line.Item.Name.Contains(search))));
        }

        if (filters.InvoiceId.HasValue)
        {
            query = query.Where(line =>
                line.InvoiceId == filters.InvoiceId.Value);
        }

        if (filters.ItemId.HasValue)
        {
            query = query.Where(line => line.ItemId == filters.ItemId.Value);
        }

        if (filters.InvoiceType.HasValue)
        {
            query = query.Where(line =>
                line.Invoice.InvoiceType == filters.InvoiceType.Value);
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(line =>
                line.Invoice.InvoiceDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(line =>
                line.Invoice.InvoiceDate <= filters.ToDate.Value);
        }

        return query;
    }

    private static IQueryable<LineProjection> ProjectLines(
        IQueryable<InvoiceLine> query) =>
        query.Select(line => new LineProjection
        {
            InvoiceLineId = line.Id,
            InvoiceId = line.InvoiceId,
            InvoiceNumber = line.Invoice.InvoiceNumber,
            InvoiceDate = line.Invoice.InvoiceDate,
            InvoiceType = line.Invoice.InvoiceType,
            BusinessPartnerId = line.Invoice.BusinessPartnerId,
            BusinessPartnerName = line.Invoice.BusinessPartner.Name,
            StoreId = line.Invoice.StoreId,
            StoreName = line.Invoice.Store.Name,
            ItemId = line.ItemId!.Value,
            ItemCode = line.Item!.Code,
            ItemName = line.Item.Name,
            ItemUnitName = line.ItemUnit != null
                ? line.ItemUnit.Name
                : line.Item.ItemUnit.Name,
            Quantity = line.Quantity,
            InvoiceCurrency = line.Invoice.Currency,
            InvoiceUnitPrice = line.Price,
            BaseInvoiceUnitPrice = line.BaseUnitPrice
        });

    private async Task<IReadOnlyList<InvoiceItemPricingRowResponse>> BuildRowsAsync(
        IReadOnlyList<LineProjection> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var invoiceIds = lines
            .Select(line => line.InvoiceId)
            .Distinct()
            .ToArray();
        var itemIds = lines
            .Select(line => line.ItemId)
            .Distinct()
            .ToArray();
        var movementTypes = InvoiceMovementTypes;

        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                invoiceIds.Contains(movement.ReferenceId) &&
                itemIds.Contains(movement.ItemId) &&
                movementTypes.Contains(movement.MovementType))
            .Select(movement => new MovementProjection
            {
                InvoiceId = movement.ReferenceId,
                ItemId = movement.ItemId,
                MovementType = movement.MovementType,
                CostStatus = movement.CostStatus,
                UnitCost = movement.UnitCost,
                AverageCostAfter = movement.AverageCostAfter
            })
            .ToListAsync(cancellationToken);

        var movementsByKey = movements.ToDictionary(
            movement => (
                movement.InvoiceId,
                movement.ItemId,
                movement.MovementType));

        var expensesByItem = await LoadItemExpensesAsync(
            itemIds,
            cancellationToken);

        return lines
            .Select(line =>
            {
                movementsByKey.TryGetValue(
                    (
                        line.InvoiceId,
                        line.ItemId,
                        InvoiceMovementRules.GetItemMovementType(
                            line.InvoiceType)),
                    out var movement);
                expensesByItem.TryGetValue(line.ItemId, out var itemExpenses);

                return BuildRow(
                    line,
                    movement,
                    itemExpenses ?? []);
            })
            .ToArray();
    }

    private static InvoiceItemPricingRowResponse BuildRow(
        LineProjection line,
        MovementProjection? movement,
        IReadOnlyList<ItemPricingExpenseResponse> expenses)
    {
        var expenseResponses = expenses.ToArray();
        var manualExpensesPerUnit = InventoryCostRules.RoundValue(
            expenseResponses.Sum(expense => expense.Amount));
        var manualExpensesTotal = InventoryCostRules.CalculateTotal(
            line.Quantity,
            manualExpensesPerUnit);

        decimal? averageCost = null;
        if (movement is not null)
        {
            averageCost = InvoiceMovementRules.IsInbound(line.InvoiceType)
                ? movement.AverageCostAfter
                : movement.UnitCost ?? movement.AverageCostAfter;
        }

        var indicativeUnitCost = averageCost.HasValue
            ? InventoryCostRules.RoundUnitCost(
                averageCost.Value + manualExpensesPerUnit)
            : (decimal?)null;
        var indicativeTotalCost = indicativeUnitCost.HasValue
            ? InventoryCostRules.CalculateTotal(
                line.Quantity,
                indicativeUnitCost.Value)
            : (decimal?)null;

        return new InvoiceItemPricingRowResponse(
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
            InvoiceCurrency: line.InvoiceCurrency,
            InvoiceUnitPrice: line.InvoiceUnitPrice,
            BaseInvoiceUnitPrice: line.BaseInvoiceUnitPrice,
            CostStatus: movement?.CostStatus,
            InventoryUnitCost: movement?.UnitCost,
            AverageCost: averageCost,
            ManualExpensesTotal: manualExpensesTotal,
            ManualExpensesPerUnit: manualExpensesPerUnit,
            IndicativeUnitCost: indicativeUnitCost,
            IndicativeTotalCost: indicativeTotalCost,
            Expenses: expenseResponses);
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken)
        ?? CurrencyCode.EGP;

    private static Error? Validate(
        PaginationRequest pagination,
        InvoiceItemPricingFilterRequest filters)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return PaginationErrors.Invalid();
        }

        if (filters.Search?.Trim().Length > 200)
        {
            return InvalidFilters(
                "لا يمكن أن يتجاوز البحث 200 حرف.",
                nameof(filters.Search));
        }

        if (filters.InvoiceId is <= 0)
        {
            return InvalidFilters(
                "رقم الفاتورة غير صالح.",
                nameof(filters.InvoiceId));
        }

        if (filters.ItemId is <= 0)
        {
            return InvalidFilters(
                "رقم الصنف غير صالح.",
                nameof(filters.ItemId));
        }

        if (filters.InvoiceType.HasValue &&
            !Enum.IsDefined(filters.InvoiceType.Value))
        {
            return InvalidFilters(
                "نوع الفاتورة غير صالح.",
                nameof(filters.InvoiceType));
        }

        if (filters.FromDate.HasValue &&
            filters.ToDate.HasValue &&
            filters.FromDate.Value > filters.ToDate.Value)
        {
            return InvalidFilters(
                "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.",
                nameof(filters.ToDate));
        }

        return null;
    }

    private sealed class LineProjection
    {
        public int InvoiceLineId { get; init; }
        public int InvoiceId { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public DateOnly InvoiceDate { get; init; }
        public InvoiceType InvoiceType { get; init; }
        public int BusinessPartnerId { get; init; }
        public string BusinessPartnerName { get; init; } = string.Empty;
        public int StoreId { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public int ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string ItemUnitName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public CurrencyCode InvoiceCurrency { get; init; }
        public decimal InvoiceUnitPrice { get; init; }
        public decimal BaseInvoiceUnitPrice { get; init; }
    }

    private sealed class MovementProjection
    {
        public int InvoiceId { get; init; }
        public int ItemId { get; init; }
        public ItemMovementType MovementType { get; init; }
        public InventoryCostStatus CostStatus { get; init; }
        public decimal? UnitCost { get; init; }
        public decimal AverageCostAfter { get; init; }
    }

    private async Task<Dictionary<int, IReadOnlyList<ItemPricingExpenseResponse>>>
        LoadItemExpensesAsync(
            IReadOnlyCollection<int> itemIds,
            CancellationToken cancellationToken)
    {
        var expenses = await dbContext.ItemPricingExpenses
            .AsNoTracking()
            .Where(expense =>
                expense.CompanyId == companyId &&
                itemIds.Contains(expense.ItemId))
            .OrderBy(expense => expense.Id)
            .Select(expense => new
            {
                expense.ItemId,
                expense.Id,
                expense.Name,
                expense.Amount,
                expense.Notes
            })
            .ToListAsync(cancellationToken);

        return expenses
            .GroupBy(expense => expense.ItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ItemPricingExpenseResponse>)group
                    .Select(expense => new ItemPricingExpenseResponse(
                        Id: expense.Id,
                        Name: expense.Name,
                        Amount: expense.Amount,
                        Notes: expense.Notes))
                    .ToArray());
    }
}
