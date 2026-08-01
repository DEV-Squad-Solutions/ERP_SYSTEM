using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Payroll
{
    public sealed class PayrollEntry : AuditableEntity
    {
        public int Id { get; set; }

        public int PayrollPeriodId { get; set; }
        public PayrollPeriod PayrollPeriod { get; set; } = default!;

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = default!;

        // Employee Snapshot
        public string EmployeeCode { get; set; } = default!;
        public string EmployeeName { get; set; } = default!;
        public EmployeeType EmployeeType { get; set; }

        // Attendance Summary
        public int DaysWorked { get; set; }
        public int AbsentDays { get; set; }
        public decimal OvertimeHours { get; set; }

        // Manual Transactions
        public decimal TotalCredits { get; set; } = default!;
        public decimal TotalDebits { get; set; } = default!;

        // Payroll
        public decimal? GrossSalary { get; set; } = default!;// Gross Salary before deductions
        public decimal? NetSalary { get; set; } = default!;// Net Salary after deductions
    }
}
