using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed record CashboxFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    CurrencyCode? Currency = null,
    bool? IsActive = null);
