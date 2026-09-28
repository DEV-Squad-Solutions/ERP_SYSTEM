using MiniErp.Domain.Enums;

namespace MiniErp.Application.Common.Models;

public sealed record FiscalYearQueryScope(
    int FiscalYearId,
    string FiscalYearName,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearStatus Status,
    bool IsCurrent);
