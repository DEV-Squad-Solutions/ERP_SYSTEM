using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public sealed record AutomaticJournalEntryRequest(
    int FiscalYearId,
    DateOnly EntryDate,
    string Description,
    JournalEntrySourceType SourceType,
    int SourceId,
    string? SourceNumber,
    IReadOnlyList<JournalEntryLineRequest> Lines);

public sealed record AutomaticJournalEntryResult(
    int JournalEntryId,
    string EntryNumber,
    bool Created);

public interface IAutomaticPostingService
{
    Task<Result<AutomaticJournalEntryResult>> CreateOrGetAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AutomaticJournalEntryResult>> CreateOrUpdateAsync(
        AutomaticJournalEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default);
}
