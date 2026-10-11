using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Users;

public sealed class CashboxRealtimeAudience(ApplicationDbContext dbContext) : ICashboxRealtimeAudience, IScopedService
{
    public async Task<Guid[]> ResolveAsync(string resource, RealtimeJobRequest request, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(request.EntityId, out var id)) return [];
        int[]? cashboxIds = null;
        // Explicit company constraints are required because soft-deleted records must still resolve on delete events.
        switch (resource)
        {
            case "Cashbox":
                cashboxIds = await dbContext.Cashboxes.IgnoreQueryFilters().AsNoTracking()
                    .Where(row => row.CompanyId == request.CompanyId && row.Id == id)
                    .Select(row => row.Id).ToArrayAsync(cancellationToken);
                if (cashboxIds.Length == 0) cashboxIds = null;
                break;
            case "CashVoucher":
                var voucher = await dbContext.CashVouchers.IgnoreQueryFilters().AsNoTracking()
                    .Where(row => row.CompanyId == request.CompanyId && row.Id == id)
                    .Select(row => new { row.CashboxId, row.CashboxTransferId }).SingleOrDefaultAsync(cancellationToken);
                cashboxIds = voucher?.CashboxId is int cashboxId ? [cashboxId] : null;
                if (voucher?.CashboxTransferId is int transferId)
                {
                    var voucherTransfer = await dbContext.CashboxTransfers.IgnoreQueryFilters().AsNoTracking()
                        .Where(row => row.CompanyId == request.CompanyId && row.Id == transferId)
                        .Select(row => new { row.SourceCashboxId, row.DestinationCashboxId }).SingleOrDefaultAsync(cancellationToken);
                    cashboxIds = cashboxIds is null || voucherTransfer is null ? null
                        : cashboxIds.Concat([voucherTransfer.SourceCashboxId, voucherTransfer.DestinationCashboxId]).Distinct().ToArray();
                }
                break;
            case "CashboxTransfer":
                var transfer = await dbContext.CashboxTransfers.IgnoreQueryFilters().AsNoTracking()
                    .Where(row => row.CompanyId == request.CompanyId && row.Id == id)
                    .Select(row => new { row.SourceCashboxId, row.DestinationCashboxId }).SingleOrDefaultAsync(cancellationToken);
                cashboxIds = transfer is null ? null : [transfer.SourceCashboxId, transfer.DestinationCashboxId];
                break;
            case "Invoice":
                if (await dbContext.Invoices.IgnoreQueryFilters().AnyAsync(row => row.CompanyId == request.CompanyId && row.Id == id, cancellationToken))
                {
                    var directIds = await dbContext.CashVouchers.IgnoreQueryFilters().AsNoTracking()
                        .Where(row => row.CompanyId == request.CompanyId && row.InvoiceId == id)
                        .Select(row => row.CashboxId).ToArrayAsync(cancellationToken);
                    var allocatedIds = await dbContext.InvoicePayments.IgnoreQueryFilters().AsNoTracking()
                        .Where(row => row.CompanyId == request.CompanyId && row.InvoiceId == id &&
                            row.CashVoucher.CompanyId == request.CompanyId)
                        .Select(row => row.CashVoucher.CashboxId).ToArrayAsync(cancellationToken);
                    cashboxIds = directIds.Concat(allocatedIds).Any(cashboxId => !cashboxId.HasValue) ? null
                        : directIds.Concat(allocatedIds).Select(cashboxId => cashboxId!.Value).Distinct().ToArray();
                }
                break;
            case "JournalEntry":
                if (await dbContext.JournalEntries.IgnoreQueryFilters().AnyAsync(row => row.CompanyId == request.CompanyId && row.Id == id, cancellationToken))
                {
                    cashboxIds = await dbContext.JournalEntryLines.IgnoreQueryFilters().AsNoTracking()
                        .Where(row => row.CompanyId == request.CompanyId && row.JournalEntryId == id &&
                            row.PartyType == JournalPartyType.Cashbox && row.PartyId.HasValue)
                        .Select(row => row.PartyId!.Value).Distinct().ToArrayAsync(cancellationToken);
                    var unresolved = await dbContext.JournalEntryLines.IgnoreQueryFilters().AsNoTracking()
                        .AnyAsync(line => line.CompanyId == request.CompanyId && line.JournalEntryId == id &&
                            ((line.PartyType == JournalPartyType.Cashbox && !line.PartyId.HasValue) ||
                             (line.PartyType != JournalPartyType.Cashbox && dbContext.AccountMappings.IgnoreQueryFilters().Any(mapping =>
                                 mapping.CompanyId == request.CompanyId && mapping.FiscalYearId == line.JournalEntry.FiscalYearId &&
                                 mapping.MappingType == AccountingMappingType.Cashbox && mapping.AccountId == line.AccountId))), cancellationToken);
                    if (unresolved) cashboxIds = null;
                }
                break;
        }

        var memberships = await dbContext.UserCompanies.AsNoTracking()
            .Where(row => row.CompanyId == request.CompanyId)
            .Select(row => new { row.UserId, row.AllCashboxes }).ToListAsync(cancellationToken);
        var admins = await dbContext.UserRoles.AsNoTracking()
            .Join(dbContext.Roles, row => row.RoleId, role => role.Id, (row, role) => new { row.UserId, role.Name })
            .Where(row => row.Name == ApplicationRoles.Admin).Select(row => row.UserId).ToArrayAsync(cancellationToken);
        var grants = await dbContext.UserCashboxPermissions.AsNoTracking()
            .Where(row => row.CompanyId == request.CompanyId && (row.CanView || row.CanExecute))
            .Select(row => new { row.UserId, row.CashboxId }).ToListAsync(cancellationToken);
        var views = grants.ToLookup(row => row.UserId, row => row.CashboxId);
        // Unknown/deleted-unresolvable resources are full-access only, never company broadcasts.
        return memberships.Where(member => member.AllCashboxes || admins.Contains(member.UserId) ||
            (cashboxIds is not null && cashboxIds.All(cashboxId => views[member.UserId].Contains(cashboxId))))
            .Select(member => member.UserId).ToArray();
    }
}
