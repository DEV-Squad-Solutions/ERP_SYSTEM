using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries
{
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
        decimal? Deduction = null,
        bool? IsSalaryMoveToEmployeeAccount = null,
        int? CashboxId = null,
        int? CashMovementTypeId = null
        );

    public sealed record BulkPayrollEntryCreateRequest(
        List<IndividualPayrollEntryCreateRequest> Entries,
        DateOnly? DefaultStartDate = null,
        DateOnly? DefaultEndDate = null,
        bool? DefaultIsSalaryMoveToEmployeeAccount = null,
        int? DefaultCashboxId = null,
        int? DefaultCashMovementTypeId = null
        );

    public sealed record IndividualPayrollEntryCreateRequest(
        int EmployeeId,
        DateOnly? StartDate = null,
        DateOnly? EndDate = null,
        decimal? Bonus = null,
        decimal? Deduction = null,
        bool? IsSalaryMoveToEmployeeAccount = null,
        int? CashboxId = null,
        int? CashMovementTypeId = null
        );

    public sealed record PayrollEntryUpdateRequest(
        int EmployeeId,
        DateOnly? StartDate = null,
        DateOnly? EndDate = null,
        decimal? Bonus = null,
        decimal? Deduction = null,
        bool? IsSalaryMoveToEmployeeAccount = null,
        int? CashboxId = null,
        int? CashMovementTypeId = null
        );
}
