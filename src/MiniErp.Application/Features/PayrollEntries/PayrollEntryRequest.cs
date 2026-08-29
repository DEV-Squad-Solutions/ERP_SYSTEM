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
    DateOnly? StartDate,
    DateOnly? EndDate,
    int EmployeeId,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkPayrollEntryCreateRequest(
    List<IndividualPayrollEntryCreateRequest> Entries,
    DateOnly? DefaultStartDate = null,
    DateOnly? DefaultEndDate = null);

public sealed record IndividualPayrollEntryCreateRequest(
    int EmployeeId,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record PayrollEntryUpdateRequest(
    int EmployeeId,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkPayrollEntryUpdateRequest(
    List<IndividualPayrollEntryUpdateRequest> Entries);

public sealed record IndividualPayrollEntryUpdateRequest(
    int Id,
    int EmployeeId,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkPayrollEntryDeleteRequest(
    List<int> PayrollEntryIds);
