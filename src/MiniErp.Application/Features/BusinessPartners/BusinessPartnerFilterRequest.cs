using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    string? TaxNumber = null,
    CurrencyCode? Currency = null,
    bool? IsActive = null,
    bool? Special = null);
