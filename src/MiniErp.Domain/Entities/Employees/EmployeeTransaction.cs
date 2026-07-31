using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Entities.Employees
{
    public sealed class EmployeeTransaction : AuditableEntity
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = default!;

        public EmployeeTransactionType Type { get; set; }
        public decimal Amount { get; set; } = default!;
        public DateOnly TransactionDate { get; set; }

        public string? Notes { get; set; } = default!;

        public bool IsProcessed { get; set; } // Indicates whether the transaction has been processed in payroll
        public int? PayrollEntryId { get; set; }
    }
}
