using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FiscalYears;

public sealed record FiscalYearFilterRequest(
    string? Search = null,
    FiscalYearStatus? Status = null,
    bool? IsCurrent = null);
