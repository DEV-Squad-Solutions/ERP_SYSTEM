using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollPeriods;

public sealed record PayrollPeriodCreateRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int WorkingDaysInPeriod = 26,
    string? Name = null);

public sealed record PayrollPeriodUpdateRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int WorkingDaysInPeriod = 26,
    string? Name = null,
    PayrollPeriodStatus? Status = null);

public sealed record PayrollPeriodFilterRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    PayrollPeriodStatus? Status = null,
    string? Search = null);

public sealed record PayrollPeriodReportByDateRangeRequest(
    DateOnly StartDate,
    DateOnly EndDate);
