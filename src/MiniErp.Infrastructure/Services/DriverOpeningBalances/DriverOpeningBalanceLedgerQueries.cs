using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.DriverOpeningBalances;

internal static class DriverOpeningBalanceLedgerQueries
{
    public static IQueryable<JournalEntryLine> PostedOpeningLines(
        ApplicationDbContext dbContext, int companyId, int fiscalYearId) =>
        dbContext.JournalEntryLines.AsNoTracking().Where(line =>
            line.CompanyId == companyId &&
            line.JournalEntry.CompanyId == companyId &&
            line.JournalEntry.FiscalYearId == fiscalYearId &&
            line.PartyType == JournalPartyType.Driver &&
            line.PartyId > 0 &&
            line.JournalEntry.EntryType == JournalEntryType.Opening &&
            line.JournalEntry.Status == JournalEntryStatus.Posted &&
            line.JournalEntry.ReversalOfEntryId == null &&
            line.JournalEntry.ReversedOn == null);

    public static IQueryable<JournalEntryLine> LegacyOpeningLines(
        ApplicationDbContext dbContext, int companyId, int fiscalYearId, DateOnly startDate) =>
        PostedOpeningLines(dbContext, companyId, fiscalYearId).Where(line =>
            line.JournalEntry.SourceType == null &&
            line.JournalEntry.EntryDate == startDate);

    public static IQueryable<JournalEntryLine> CarriedLines(
        ApplicationDbContext dbContext, int companyId, int fiscalYearId) =>
        PostedOpeningLines(dbContext, companyId, fiscalYearId)
            .Where(line => line.JournalEntry.SourceType == JournalEntrySourceType.FiscalYearClosing);
}
