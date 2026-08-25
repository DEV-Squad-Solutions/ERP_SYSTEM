using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Employees;

public sealed class EmployeeTransaction : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>Credit increases what the company owes; Debit decreases it.</summary>
    public EmployeeTransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public DateOnly TransactionDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>Running account balance after this entry (Credits − Debits).</summary>
    public decimal RunningBalance { get; set; }

    /// <summary>Where this entry originated (manual entry, payroll posting, etc.).</summary>
    public EmployeeTransactionSource SourceType { get; set; } = EmployeeTransactionSource.Manual;

    /// <summary>PK of the source document (e.g. PayrollEntry.Id when SourceType = Payroll).</summary>
    public int? SourceId { get; set; }

    /// <summary>
    /// Mandatory link to the CashVoucher that was generated for this transaction.
    /// Every operation on an employee account is backed by a Cash Voucher.
    /// </summary>
    public int CashVoucherId { get; set; }
    public CashVoucher CashVoucher { get; set; } = null!;

    /// <summary>
    /// Mandatory link to the Cashbox associated with this transaction's cash voucher.
    /// </summary>
    public int CashBoxId { get; set; }
    public Cashbox Cashbox { get; set; } = null!;
}
