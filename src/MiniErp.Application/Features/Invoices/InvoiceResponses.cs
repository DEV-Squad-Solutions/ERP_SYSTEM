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
    int? DriverId,
    string? DriverName,
    int? ActualDriverId,
    string? ActualDriverName,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    PaymentStatus PaymentStatus,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Notes,
    int LineCount,
    int ContainerLineCount,
    byte[] RowVersion);

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
    int? DriverId,
    string? DriverName,
    int? ActualDriverId,
    string? ActualDriverName,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    PaymentStatus PaymentStatus,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Notes,
    byte[] RowVersion,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<InvoiceContainerLineResponse> ContainerLines);
