using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountingReadiness;

public sealed record AccountingReadinessSourceSummary(
    JournalEntrySourceType SourceType,
    int TotalSources,
    int PostedSources,
    int MissingJournalSources);

public sealed record AccountingReadinessIssue(
    string IssueType,
    JournalEntrySourceType? SourceType,
    int? SourceId,
    string? SourceNumber,
    DateOnly? SourceDate,
    AccountingMappingType? MappingType,
    int? MappingSourceId,
    string Message);

public sealed record AccountingReadinessResponse(
    int FiscalYearId,
    string FiscalYearName,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsReady,
    int TotalSources,
    int PostedSources,
    int MissingJournalSources,
    int OrphanAutomaticJournals,
    int DuplicateAutomaticJournals,
    int UnbalancedAutomaticJournals,
    int PendingInventoryCosts,
    int MissingOrInvalidMappings,
    int DeferredPayrollSources,
    IReadOnlyList<AccountingReadinessSourceSummary> Sources,
    IReadOnlyList<AccountingReadinessIssue> Issues);

public sealed record AccountingBackfillResponse(
    int FiscalYearId,
    int ProcessedSources,
    int CreatedJournals,
    int UpdatedJournals,
    int DeferredPayrollSources,
    AccountingReadinessResponse Readiness);
