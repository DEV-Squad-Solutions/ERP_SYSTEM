using Microsoft.EntityFrameworkCore;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

/// <summary>
/// Documents that store an exchange-rate id. Deleted documents count, so a
/// rate that ever backed a document keeps its identity.
/// </summary>
internal static class ExchangeRateReferences
{
    public static async Task<bool> IsReferencedAsync(
        ApplicationDbContext dbContext,
        int companyId,
        int exchangeRateId,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.ExchangeRateId == exchangeRateId,
                cancellationToken) ||
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.ExchangeRateId == exchangeRateId,
                cancellationToken) ||
        await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.ExchangeRateId == exchangeRateId,
                cancellationToken) ||
        await dbContext.EmployeeOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.ExchangeRateId == exchangeRateId,
                cancellationToken) ||
        await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .AnyAsync(
                cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.OpeningExchangeRateId == exchangeRateId,
                cancellationToken);
}
