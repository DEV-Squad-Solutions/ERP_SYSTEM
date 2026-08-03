using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Payroll
{
    public sealed class PayrollPeriod : AuditableEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public string Name { get; set; } = default!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public PayrollPeriodStatus Status { get; set; }
        public int WorkingDaysInPeriod { get; set; } = 26;
        public int? TotalEmployees { get; set; }
        public int? TotalMonthlyEmployees { get; set; }
        public int? TotalDailyEmployees { get; set; }

        // Salary Summary
        public decimal? TotalGrossSalary { get; set; }
        public decimal? TotalCredits { get; set; }
        public decimal? TotalDebits { get; set; }
        public decimal? TotalNetSalary { get; set; }

        // Attendance Summary
        public decimal? TotalWorkedDays { get; set; }
        public decimal? TotalOvertimeDays { get; set; }
        public decimal? TotalAbsentDays { get; set; }

        // Process Information
        public DateTime? CalculatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        private static string GenerateName(DateOnly startDate, DateOnly endDate)
        {
            return startDate.Month == endDate.Month &&
                   startDate.Year == endDate.Year
                ? startDate.ToString("MMMM yyyy")
                : $"{startDate:MMMM yyyy} - {endDate:MMMM yyyy}";
        }
    }
}
