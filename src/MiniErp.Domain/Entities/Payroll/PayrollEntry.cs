using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using System;

namespace MiniErp.Domain.Entities.Payroll
{
    public sealed class PayrollEntry : AuditableEntity
    {
        public int Id { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = default!;

        // Employee Snapshot
        public string EmployeeCode { get; set; } = default!;
        public string EmployeeName { get; set; } = default!;
        public EmployeeType EmployeeType { get; set; }

        // Attendance Summary
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public decimal WorkedDaysbydayunit { get; set; }
        public decimal? Overtimebydayunit { get; set; }
        public decimal? Deductionbydayunit { get; set; }
        public decimal? RequiredWorkingDays { get; set; } = 26;

        // Manual Transactions / Adjustments
        public decimal Bonus { get; set; } = default!;
        public decimal Deduction { get; set; } = default!;

        // Salary Calculations
        public decimal? SalaryPerDay { get; set; }
        public decimal CalculatedSalary { get; set; }

        // Payroll Status
        public decimal? GrossSalary { get; set; } // Gross Salary before deductions
        public decimal? NetSalary { get; set; }   // Net Salary after deductions
        public bool IsTakeSalary { get; set; }

        // Treasury / Cash box movement link
        public int? CashVoucherId { get; set; }
        public CashVoucher? CashVoucher { get; set; }
    }
}
