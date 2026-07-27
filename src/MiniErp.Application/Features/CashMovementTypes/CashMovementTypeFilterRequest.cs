using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed record CashMovementTypeFilterRequest(
    string? Search = null,
    string? Name = null,
    CashDirection? Direction = null,
    bool? ForPartner = null,
    bool? IsActive = null);

public sealed record CashMovementTypeSelectRequest(
    CashDirection? Direction = null,
    bool? ForPartner = null);
