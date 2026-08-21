using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries
{
    public sealed record PayrollEntryFilterRequest(
    int? EmployeeId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool? IsTakeSalary = null,
    EmployeeType? EmployeeType = null,
    string? Search = null);

    public sealed record PayrollEntryCreateRequest(
        DateOnly? StartDate,
        DateOnly? EndDate,
        int CashboxId,
        int CashMovementTypeId,
        int EmployeeId,
        decimal? Bonus = null,
        decimal? Deduction = null);

    //public sealed record PayrollEntryUpdateRequest(
    //    int EmployeeId,
    //    DateOnly? StartDate,
    //    DateOnly? EndDate,
    //    decimal? Bonus = 0,
    //    decimal? Deduction = 0,
    //    decimal? Overtimebydayunit = 0,
    //    int? CashboxId = null,
    //    int? CashMovementTypeId = null);

}
