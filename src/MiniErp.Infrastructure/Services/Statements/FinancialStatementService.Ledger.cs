using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    /// <summary>
    /// Canonical source for financial reports. Global query filters exclude
    /// soft-deleted lines and entries; the explicit predicates also make the
    /// company and posting rules visible at the reporting boundary.
    /// </summary>
    private IQueryable<JournalEntryLine> PostedLedgerLines() =>
        PostedJournalLedgerLines.Create(dbContext, companyId);

    private Task<FiscalYearScope?> ResolveFiscalYearAsync(
        int? fiscalYearId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                (fiscalYearId.HasValue
                    ? year.Id == fiscalYearId.Value
                    : year.IsCurrent))
            .Select(year => new FiscalYearScope(
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record FiscalYearScope(
        int Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate);
}

internal static class PostedJournalLedgerLines
{
    public static IQueryable<JournalEntryLine> Create(
        ApplicationDbContext dbContext,
        int companyId) =>
        dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                !line.IsDeleted &&
                line.JournalEntry.CompanyId == companyId &&
                !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null);
}
