using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record OutCompanyPayrollEntryRequest(
    int EmployeeId,
    DateOnly StartDate,
    DateOnly EndDate,
    int PresentDays,
    decimal WorkedDaysByDayUnit,
    decimal? OvertimeByDayUnit = null,
    decimal? DeductionByDayUnit = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkOutCompanyPayrollEntryRequest(
    List<IndividualOutCompanyPayrollEntryRequest> Entries,
    DateOnly? DefaultStartDate = null,
    DateOnly? DefaultEndDate = null);

public sealed record IndividualOutCompanyPayrollEntryRequest(
    int EmployeeId,
    int PresentDays,
    decimal WorkedDaysByDayUnit,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? OvertimeByDayUnit = null,
    decimal? DeductionByDayUnit = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record OutCompanyPayrollEntryUpdateRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int PresentDays,
    decimal WorkedDaysByDayUnit,
    decimal? OvertimeByDayUnit = null,
    decimal? DeductionByDayUnit = null,
    decimal? Bonus = null,
    decimal? Deduction = null);

public sealed record BulkOutCompanyPayrollEntryUpdateRequest(
    List<IndividualOutCompanyPayrollEntryUpdateRequest> Entries,
    DateOnly? DefaultStartDate = null,
    DateOnly? DefaultEndDate = null);

public sealed record IndividualOutCompanyPayrollEntryUpdateRequest(
    int Id,
    int PresentDays,
    decimal WorkedDaysByDayUnit,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? OvertimeByDayUnit = null,
    decimal? DeductionByDayUnit = null,
    decimal? Bonus = null,
    decimal? Deduction = null);
