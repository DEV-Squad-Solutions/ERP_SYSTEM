namespace MiniErp.Application.Features.PartnerItemReports;

public sealed record PartnerItemReportMovementResponse(
    int ItemId,
    string ItemName,
    int InvoiceId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string MovementType,
    int Count,
    decimal Quantity,
    decimal Weight,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record PartnerItemReportSummaryResponse(
    decimal TotalSalesQuantity,
    decimal TotalPurchaseQuantity,
    decimal TotalSalesWeight,
    decimal TotalPurchaseWeight);

public sealed record PartnerItemReportResponse(
    int? BusinessPartnerId,
    string? BusinessPartnerName,
    int? ItemId,
    string? ItemName,
    DateOnly? FromDate,
    DateOnly? ToDate,
    PartnerItemReportSummaryResponse Summary,
    IReadOnlyList<PartnerItemReportMovementResponse> Movements);
