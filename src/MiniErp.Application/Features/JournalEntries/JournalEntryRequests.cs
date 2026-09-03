using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public sealed record JournalEntryRequest(
    int FiscalYearId,
    DateOnly EntryDate,
    string Description,
    JournalEntryType EntryType,
    IReadOnlyList<JournalEntryLineRequest> Lines)
{
    public const int DescriptionMaximumLength = 500;
}

public sealed record JournalEntryUpdateRequest(
    int FiscalYearId,
    DateOnly EntryDate,
    string Description,
    IReadOnlyList<JournalEntryLineRequest> Lines,
    byte[]? RowVersion);

public sealed record JournalEntryLineRequest(
    int AccountId,
    string? Description,
    decimal Debit,
    decimal Credit)
{
    public const int DescriptionMaximumLength = 300;
}
