using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntryFilterRequest(
    int? EmployeeId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool? IsSalaryMoveToEmployeeAccount = null,
    EmployeeType? EmployeeType = null,
    string? Search = null);

public sealed record PayrollEntryCreateRequest(
    int EmployeeId,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkPayrollEntryCreateRequest(
    List<IndividualPayrollEntryCreateRequest> Entries,
    DateOnly? DefaultEndDate = null);

public sealed record IndividualPayrollEntryCreateRequest(
    int EmployeeId,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record PayrollEntryUpdateRequest(
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null,
    int? EmployeeId = null);

public sealed record BulkPayrollEntryUpdateRequest(
    List<IndividualPayrollEntryUpdateRequest> Entries,
    DateOnly? DefaultEndDate = null);

public sealed record IndividualPayrollEntryUpdateRequest(
    int Id,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null,
    int? EmployeeId = null);

public sealed record BulkPayrollEntryDeleteRequest(
    List<int> PayrollEntryIds);
