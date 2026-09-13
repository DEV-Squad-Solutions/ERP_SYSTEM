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
