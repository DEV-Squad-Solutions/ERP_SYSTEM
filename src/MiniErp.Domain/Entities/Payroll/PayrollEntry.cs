using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Payroll;

public sealed class PayrollEntry : AuditableEntity
{
    public int Id { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public string EmployeeCode { get; set; } = default!;
    public string EmployeeName { get; set; } = default!;
    public EmployeeType EmployeeType { get; set; }

    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public decimal WorkedDaysbydayunit { get; set; }
    public decimal? Overtimebydayunit { get; set; }
    public decimal? Deductionbydayunit { get; set; }
    public decimal? RequiredWorkingDays { get; set; }

    public decimal? Bonus { get; set; }
    public decimal? Deduction { get; set; }

    public decimal? SalaryPerDay { get; set; }
    public decimal CalculatedSalary { get; set; }

    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }

    public bool IsSalaryMoveToEmployeeAccount { get; set; }
    public DateOnly? SalaryMovedOn { get; set; }
}
