using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

public sealed class JournalEntry : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    public string EntryNumber { get; set; } = string.Empty;

    public DateOnly EntryDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public JournalEntryType EntryType { get; set; }

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;

    public DateTime PostedOn { get; set; }

    public DateTime? ReversedOn { get; set; }

    public int? ReversalOfEntryId { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<JournalEntryLine> Lines { get; set; } = [];
}
