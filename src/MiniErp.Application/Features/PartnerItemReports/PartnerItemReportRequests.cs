using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerItemReports;

public sealed record PartnerItemReportFilterRequest(
    int BusinessPartnerId,
    int? ItemId,
    string? Search = null,
    InvoiceType? MovementType = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
