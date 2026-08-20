using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public enum InvoiceReturnType
{
    SalesReturn = (int)InvoiceType.SalesReturn,
    PurchaseReturn = (int)InvoiceType.PurchaseReturn
}

public sealed record InvoiceReturnSourceFilterRequest(
    int BusinessPartnerId,
    int StoreId,
    InvoiceReturnType ReturnType,
    DateOnly AsOfDate,
    string? Search = null,
    int? CurrentReturnInvoiceId = null);

public sealed record InvoiceReturnSourceLineResponse(
    int SourceInvoiceLineId,
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    int Count,
    decimal Weight,
    decimal OriginalQuantity,
    decimal ReturnedQuantity,
    decimal AvailableQuantity,
    decimal UnitPrice,
    decimal OriginalTotal,
    InventoryCostStatus CostStatus,
    decimal PendingCostQuantity,
    decimal? UnitCost);

public sealed record InvoiceReturnSourceResponse(
    int InvoiceId,
    string InvoiceNumber,
    string? PartnerInvoiceNo,
    DateOnly InvoiceDate,
    InvoiceType InvoiceType,
    int BusinessPartnerId,
    string BusinessPartnerName,
    int StoreId,
    string StoreName,
    CurrencyCode Currency,
    decimal OriginalSubtotal,
    decimal OriginalDiscountAmount,
    decimal OriginalTotal,
    IReadOnlyList<InvoiceReturnSourceLineResponse> Lines);
