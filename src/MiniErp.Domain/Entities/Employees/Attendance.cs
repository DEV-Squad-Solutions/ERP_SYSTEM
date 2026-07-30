using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Employees
{
    public sealed class Attendance : AuditableEntity
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = default!;

        public AttendanceStatus Status { get; set; }
        public DateOnly WorkDate { get; set; }
        public TimeOnly? CheckIn { get; set; }
        public TimeOnly? CheckOut { get; set; }

        public decimal HoursWorked { get; set; }
        public decimal OvertimeHours { get; set; }

        public string? WorkLocation { get; set; } = default!;
        public string? Notes { get; set; }
    }
}
