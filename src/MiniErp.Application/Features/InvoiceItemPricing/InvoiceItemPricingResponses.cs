using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public sealed record InvoiceLinePricingExpenseResponse(
    int Id,
    string Name,
    decimal Amount,
    string? Notes);

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
    IReadOnlyList<InvoiceLinePricingExpenseResponse> Expenses);

public sealed record InvoiceItemPricingPagedResponse(
    CurrencyCode BaseCurrency,
    IReadOnlyList<InvoiceItemPricingRowResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
