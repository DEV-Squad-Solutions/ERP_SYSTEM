using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntryRequest(
    DateOnly? StartDate,
    DateOnly? EndDate,
    int EmployeeId,
    decimal Bonus = 0,
    decimal Deduction = 0,
    decimal Overtimebydayunit = 0);
