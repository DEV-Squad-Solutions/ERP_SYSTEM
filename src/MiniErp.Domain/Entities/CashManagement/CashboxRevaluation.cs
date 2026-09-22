using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.CashManagement;

/// <summary>
/// A dated, immutable revaluation request for one cashbox.  The journal entry
/// is the accounting source; this row keeps the quantities/rates used so a
/// later run can calculate only the incremental difference.
/// </summary>
public sealed class CashboxRevaluation : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    public DateOnly RevaluationDate { get; set; }

    public decimal ClosingRate { get; set; }

    public decimal ForeignAmount { get; set; }

    public decimal CarryingBaseAmount { get; set; }

    public decimal TargetBaseAmount { get; set; }

    public decimal DeltaBaseAmount { get; set; }

    public int? JournalEntryId { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
}
