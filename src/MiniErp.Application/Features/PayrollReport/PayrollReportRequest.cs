using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollReport;

public sealed record PayrollPeriodReportByDateRangeRequest(
    DateOnly StartDate,
    DateOnly EndDate);
