using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Employees
{
    public sealed class Employee : AuditableEntity
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        public EmployeeType Type { get; set; } = EmployeeType.Daily;
        public decimal? DailySalary { get; set; } // Applicable if Type is Daily
        public decimal? MonthlySalary { get; set; } // Applicable if Type is Monthly

        public bool IsActive { get; set; } = true;
    }
}
