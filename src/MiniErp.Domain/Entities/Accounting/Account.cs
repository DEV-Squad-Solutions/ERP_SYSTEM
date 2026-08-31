using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class Account : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? ParentAccountId { get; set; }

    public Account? ParentAccount { get; set; }

    public AccountType AccountType { get; set; }

    public NormalBalance NormalBalance { get; set; }

    public bool IsPosting { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<Account> Children { get; set; } = [];

    public ICollection<AccountStatementMapping> StatementMappings { get; set; } = [];
}
