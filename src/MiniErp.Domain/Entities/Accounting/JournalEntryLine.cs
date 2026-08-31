using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class JournalEntryLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int JournalEntryId { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}
