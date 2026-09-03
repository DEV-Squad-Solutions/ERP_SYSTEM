using MiniErp.Domain.Enums;
using MiniErp.Application.Features.Items;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public sealed record ItemPricingExpensesResponse(
    int ItemId,
    decimal ExpensesPerUnit,
    IReadOnlyList<ItemPricingExpenseResponse> Expenses);

public sealed record InvoiceItemPricingRowResponse(
    int InvoiceLineId,
    int InvoiceId,
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
    CurrencyCode InvoiceCurrency,
    decimal InvoiceUnitPrice,
    decimal BaseInvoiceUnitPrice,
    InventoryCostStatus? CostStatus,
    decimal? InventoryUnitCost,
    decimal? AverageCost,
    decimal ManualExpensesTotal,
    decimal ManualExpensesPerUnit,
    decimal? IndicativeUnitCost,
    decimal? IndicativeTotalCost,
    IReadOnlyList<ItemPricingExpenseResponse> Expenses);

public sealed record InvoiceItemPricingPagedResponse(
    CurrencyCode BaseCurrency,
    IReadOnlyList<InvoiceItemPricingRowResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
