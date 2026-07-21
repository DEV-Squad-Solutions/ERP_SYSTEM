using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? TaxNumber,
    CurrencyCode Currency,
    decimal CreditLimit,
    bool IsActive);
