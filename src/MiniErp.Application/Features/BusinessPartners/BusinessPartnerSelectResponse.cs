using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerSelectResponse(
    int Id,
    string Name,
    CurrencyCode Currency);
