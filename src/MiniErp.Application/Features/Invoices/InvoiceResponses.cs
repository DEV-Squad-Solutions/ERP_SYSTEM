using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public sealed record InvoiceLineResponse(
    int Id,
    int CompanyId,
    int InvoiceId,
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    int Count,
    decimal Weight,
    decimal Quantity,
    decimal Price,
    decimal Total,
    decimal BaseUnitPrice,
    decimal BaseTotal,
    string? Notes)
{
    public int? SourceInvoiceLineId { get; init; }

    public decimal? ReturnUnitCost { get; init; }

    public InventoryCostStatus? CostStatus { get; init; }

    public decimal PendingCostQuantity { get; init; }

    public decimal? UnitCost { get; init; }

    public decimal InventoryTotalCost { get; init; }

    public decimal QuantityAfter { get; init; }

    public decimal AverageCostAfter { get; init; }

    public decimal InventoryValueAfter { get; init; }
}

public sealed record InvoiceContainerLineResponse(
    int Id,
    int CompanyId,
    int InvoiceId,
    int ContainerId,
    string ContainerCode,
    string ContainerName,
    int OutgoingUnits,
    int IncomingUnits);

public sealed record InvoiceListResponse(
    int Id,
    int CompanyId,
    string InvoiceNumber,
    string? ExportInvoiceCode,
    InvoiceType InvoiceType,
    InvoiceContentType ContentType,
    PaymentTerm PaymentTerm,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    int BusinessPartnerId,
    string BusinessPartnerName,
    int StoreId,
    string StoreName,
    int? ContainerStoreId,
    string? ContainerStoreName,
    int? CountryId,
    string? CountryName,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    int? DriverId,
    string? DriverName,
    int? ActualDriverId,
    string? ActualDriverName,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    decimal WBWeight,
    decimal WBScaleDifference,
    decimal WBDiscount,
    decimal WBTotal,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal BaseSubtotal,
    decimal BaseDiscountAmount,
    decimal BaseTotal,
    decimal BasePaidAmountAtInvoiceRate,
    PaymentStatus PaymentStatus,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Notes,
    int LineCount,
    int ContainerLineCount,
    byte[] RowVersion)
{
    public string? PartnerInvoiceNo { get; init; }

    public int? ItemsCategoryId { get; init; }

    public string? ItemsCategoryName { get; init; }

    public int? PaymentVoucherId { get; init; }

    public int? CashboxId { get; init; }

    public string? CashboxName { get; init; }

    public int? CashMovementTypeId { get; init; }

    public string? CashMovementTypeName { get; init; }

    public CurrencyCode? CashboxCurrency { get; init; }

    public decimal? CashboxAmount { get; init; }

    public decimal? CashboxExchangeRate { get; init; }

    public decimal? CashboxBaseAmount { get; init; }

    public decimal? RealizedExchangeDifference { get; init; }
}

public sealed record InvoiceSummaryResponse(
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal PaidAmount,
    decimal RemainingAmount);

public sealed record InvoiceItemBalanceResponse(
    int StoreId,
    string StoreName,
    int ItemId,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    DateOnly AsOfDate,
    decimal Balance,
    decimal AverageCost,
    decimal InventoryValue);

public sealed record InvoicePagedResponse(
    IReadOnlyList<InvoiceListResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    InvoiceSummaryResponse Summary);

public sealed record InvoiceResponse(
    int Id,
    int CompanyId,
    string InvoiceNumber,
    string? ExportInvoiceCode,
    InvoiceType InvoiceType,
    InvoiceContentType ContentType,
    PaymentTerm PaymentTerm,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    int BusinessPartnerId,
    string BusinessPartnerName,
    int StoreId,
    string StoreName,
    int? ContainerStoreId,
    string? ContainerStoreName,
    int? CountryId,
    string? CountryName,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    int? DriverId,
    string? DriverName,
    int? ActualDriverId,
    string? ActualDriverName,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    decimal WBWeight,
    decimal WBScaleDifference,
    decimal WBDiscount,
    decimal WBTotal,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal BaseSubtotal,
    decimal BaseDiscountAmount,
    decimal BaseTotal,
    decimal BasePaidAmountAtInvoiceRate,
    PaymentStatus PaymentStatus,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Notes,
    byte[] RowVersion,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<InvoiceContainerLineResponse> ContainerLines)
{
    public string? PartnerInvoiceNo { get; init; }

    public int? ItemsCategoryId { get; init; }

    public string? ItemsCategoryName { get; init; }

    public int? PaymentVoucherId { get; init; }

    public int? CashboxId { get; init; }

    public string? CashboxName { get; init; }

    public int? CashMovementTypeId { get; init; }

    public string? CashMovementTypeName { get; init; }

    public CurrencyCode? CashboxCurrency { get; init; }

    public decimal? CashboxAmount { get; init; }

    public decimal? CashboxExchangeRate { get; init; }

    public decimal? CashboxBaseAmount { get; init; }

    public decimal? RealizedExchangeDifference { get; init; }
}
