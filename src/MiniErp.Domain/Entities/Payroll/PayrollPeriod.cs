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

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public string Name { get; set; } = default!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public PayrollPeriodStatus Status { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
