using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Employees
{
    public sealed class EmployeeAttendance : AuditableEntity
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = default!;

        public EmployeeAttendanceStatus Status { get; set; }
        public DateOnly WorkDate { get; set; }
        public TimeOnly? CheckIn { get; set; }
        public TimeOnly? CheckOut { get; set; }
        public TimeOnly? WorkHours { get; set; }
        public WorkDayRatio WorkDayRatio { get; set; }=WorkDayRatio.FullDay;
        public WorkDayRatio? WorkOverTimeRatio { get; set; }
        public WorkDayRatio? WorkDaysDeductionRatio { get; set; }
        public string? WorkLocation { get; set; } = default!;
        public string? Notes { get; set; }
    }
}
