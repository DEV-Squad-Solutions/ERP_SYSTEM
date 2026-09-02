using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public sealed record JournalEntryLineResponse(
    int Id,
    int AccountId,
    string AccountCode,
    string AccountName,
    string? Description,
    decimal Debit,
    decimal Credit);

public sealed record JournalEntryResponse(
    int Id,
    int CompanyId,
    int FiscalYearId,
    string FiscalYearName,
    string EntryNumber,
    DateOnly EntryDate,
    string Description,
    JournalEntryType EntryType,
    JournalEntrySourceType? SourceType,
    int? SourceId,
    string? SourceNumber,
    JournalEntryStatus Status,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTime PostedOn,
    DateTime? ReversedOn,
    int? ReversalOfEntryId,
    string? ReversalOfEntryNumber,
    int? ReversedByEntryId,
    string? ReversedByEntryNumber,
    string CreatedById,
    DateTime CreatedOn,
    byte[] RowVersion,
    IReadOnlyList<JournalEntryLineResponse> Lines);
