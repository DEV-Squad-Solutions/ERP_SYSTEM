using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerRequest(
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? TaxNumber,
    CurrencyCode Currency,
    decimal CreditLimit,
    bool IsActive = true,
    bool Special = false);
