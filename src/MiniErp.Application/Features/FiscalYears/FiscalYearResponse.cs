using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FiscalYears;

public sealed record FiscalYearResponse(
    int Id,
    int CompanyId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearStatus Status,
    bool IsCurrent,
    DateTime? ClosedOn,
    byte[] RowVersion);

public sealed record FiscalYearSelectResponse(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearStatus Status,
    bool IsCurrent);
