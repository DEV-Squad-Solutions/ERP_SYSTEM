using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Currencies;

public sealed record CurrencyOptionResponse(
    CurrencyCode Value,
    string Description);
