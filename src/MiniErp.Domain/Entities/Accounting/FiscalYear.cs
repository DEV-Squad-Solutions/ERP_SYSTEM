using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class FiscalYear : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;

    public bool IsCurrent { get; set; }

    public DateTime? ClosedOn { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<FinancialStatementLine> FinancialStatementLines { get; set; } = [];

    public ICollection<AccountStatementMapping> AccountStatementMappings { get; set; } = [];
}
