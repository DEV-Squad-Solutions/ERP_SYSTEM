using MiniErp.Application.Common.Models;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollPeriods;

public sealed record PayrollPeriodResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    PayrollPeriodStatus Status,
    int WorkingDaysInPeriod,
    int? TotalEmployees,
    int? TotalMonthlyEmployees,
    int? TotalDailyEmployees,
    decimal? TotalGrossSalary,
    decimal? TotalCredits,
    decimal? TotalDebits,
    decimal? TotalNetSalary,
    decimal? TotalWorkedDays,
    decimal? TotalOvertimeDays,
    decimal? TotalAbsentDays,
    DateTime? CalculatedAt,
    DateTime? PaidAt);

public sealed record PayrollPeriodListResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    PayrollPeriodStatus Status,
    int WorkingDaysInPeriod,
    int? TotalEmployees,
    decimal? TotalNetSalary,
    DateTime? CalculatedAt,
    DateTime? PaidAt);

public sealed record PayrollPeriodPageResponse(
    IReadOnlyList<PayrollPeriodListResponse> Periods,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
