using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Accounting;

/// <summary>
/// Stores a dated revaluation of one foreign-currency monetary balance.
/// The original foreign amount is never changed by later exchange-rate updates;
/// only the base-currency carrying amount is adjusted by the generated journal.
/// </summary>
public sealed class MonetaryAccountRevaluation : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public CurrencyCode Currency { get; set; }

    public JournalPartyType? PartyType { get; set; }

    public int? PartyId { get; set; }

    public DateOnly RevaluationDate { get; set; }

    public decimal ClosingRate { get; set; }

    public decimal ForeignAmount { get; set; }

    public decimal CarryingBaseAmount { get; set; }

    public decimal TargetBaseAmount { get; set; }

    public decimal DeltaBaseAmount { get; set; }

    public int? JournalEntryId { get; set; }

    public JournalEntry? JournalEntry { get; set; }
}
