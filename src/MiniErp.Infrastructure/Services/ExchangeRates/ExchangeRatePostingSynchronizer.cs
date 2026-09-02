using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

public sealed class ExchangeRatePostingSynchronizer(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInvoicePostingService invoicePostingService,
    ICashVoucherPostingService cashVoucherPostingService,
    ICashboxTransferPostingService cashboxTransferPostingService,
    IOpeningBalancePostingService openingBalancePostingService)
    : IExchangeRatePostingSynchronizer, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> SynchronizeAsync(
        int exchangeRateId,
        CancellationToken cancellationToken = default)
    {
        var directInvoiceIds = dbContext.Invoices
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.ExchangeRateId == exchangeRateId)
            .Select(invoice => invoice.Id);
        var paymentInvoiceIds = dbContext.InvoicePayments
            .Where(payment =>
                payment.CompanyId == companyId &&
                (payment.Invoice.ExchangeRateId == exchangeRateId ||
                 payment.CashVoucher.ExchangeRateId == exchangeRateId))
            .Select(payment => payment.InvoiceId);
        var invoiceIds = await directInvoiceIds
            .Concat(paymentInvoiceIds)
            .Distinct()
            .ToListAsync(cancellationToken);

        var standaloneVouchers = await dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.ExchangeRateId == exchangeRateId &&
                voucher.IsPosted &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue)
            .ToListAsync(cancellationToken);
        var transferIds = await dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.ExchangeRateId == exchangeRateId &&
                voucher.CashboxTransferId.HasValue)
            .Select(voucher => voucher.CashboxTransferId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var partnerOpeningIds = await dbContext.PartnerOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.ExchangeRateId == exchangeRateId)
            .Select(balance => balance.Id)
            .ToListAsync(cancellationToken);
        var employeeOpeningIds = await dbContext.EmployeeOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.ExchangeRateId == exchangeRateId)
            .Select(balance => balance.Id)
            .ToListAsync(cancellationToken);
        var cashboxIds = await dbContext.Cashboxes
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.OpeningExchangeRateId == exchangeRateId)
            .Select(cashbox => cashbox.Id)
            .ToListAsync(cancellationToken);

        foreach (var invoiceId in invoiceIds)
        {
            var result = await invoicePostingService.SynchronizeAsync(
                invoiceId,
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Errors);
            }
        }

        foreach (var voucher in standaloneVouchers)
        {
            var result = await cashVoucherPostingService.SynchronizeAsync(
                voucher,
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Errors);
            }
        }

        foreach (var transferId in transferIds)
        {
            var result = await cashboxTransferPostingService.SynchronizeAsync(
                transferId,
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Errors);
            }
        }

        foreach (var openingId in partnerOpeningIds)
        {
            var result = await openingBalancePostingService
                .SynchronizePartnerAsync(openingId, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        foreach (var openingId in employeeOpeningIds)
        {
            var result = await openingBalancePostingService
                .SynchronizeEmployeeAsync(openingId, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        foreach (var cashboxId in cashboxIds)
        {
            var result = await openingBalancePostingService
                .SynchronizeCashboxAsync(cashboxId, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
