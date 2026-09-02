using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class AccountStatementMapping : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    public FinancialStatementType StatementType { get; set; }

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public int FinancialStatementLineId { get; set; }

    public FinancialStatementLine FinancialStatementLine { get; set; } = null!;
}
