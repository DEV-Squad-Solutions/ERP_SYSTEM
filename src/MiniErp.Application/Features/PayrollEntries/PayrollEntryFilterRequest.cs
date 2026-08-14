using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntryFilterRequest(
    int? EmployeeId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool? IsTakeSalary = null,
    EmployeeType? EmployeeType = null,
    string? Search = null);
