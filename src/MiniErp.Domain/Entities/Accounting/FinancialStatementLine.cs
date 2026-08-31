using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class FinancialStatementLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    public FinancialStatementType StatementType { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? ParentLineId { get; set; }

    public FinancialStatementLine? ParentLine { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsAssignable { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<FinancialStatementLine> Children { get; set; } = [];

    public ICollection<AccountStatementMapping> AccountMappings { get; set; } = [];
}
