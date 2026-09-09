using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

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

    public JournalPartyType? PartyType { get; set; }

    public int? PartyId { get; set; }

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>Currency of the amount entered on this line.</summary>
    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    /// <summary>Number of base-currency units represented by one currency unit.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Debit amount in <see cref="Currency"/> (the original transaction amount).</summary>
    public decimal TransactionDebit { get; set; }

    /// <summary>Credit amount in <see cref="Currency"/> (the original transaction amount).</summary>
    public decimal TransactionCredit { get; set; }
}
