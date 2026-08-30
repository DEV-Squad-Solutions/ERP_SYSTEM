using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

internal static class ExchangeRateCascadeUpdater
{
    internal static async Task<Error?> UpdateAsync(
        ApplicationDbContext dbContext,
        int companyId,
        TimeProvider timeProvider,
        int exchangeRateId,
        decimal exchangeRate,
        Error invalidLinkedTransferError,
        CancellationToken cancellationToken)
    {
        var roundedRate = Domain.Entities.Companies.ExchangeRateRules
            .RoundRate(exchangeRate);
        var rate = await dbContext.ExchangeRates
            .SingleAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == exchangeRateId,
                cancellationToken);

        if (rate.Rate == roundedRate)
        {
            return null;
        }

        var invoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Include(invoice => invoice.Lines)
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.ExchangeRateId == exchangeRateId)
            .ToListAsync(cancellationToken);
        var linkedVouchers = await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.ExchangeRateId == exchangeRateId)
            .ToListAsync(cancellationToken);
        var transferIds = linkedVouchers
            .Where(voucher => voucher.CashboxTransferId.HasValue)
            .Select(voucher => voucher.CashboxTransferId!.Value)
            .Distinct()
            .ToArray();
        var transferVouchers = transferIds.Length == 0
            ? []
            : await dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashboxTransferId.HasValue &&
                    transferIds.Contains(voucher.CashboxTransferId.Value))
                .ToListAsync(cancellationToken);
        var transferVoucherGroups = transferVouchers
            .GroupBy(voucher => voucher.CashboxTransferId!.Value)
            .ToArray();
        if (transferVoucherGroups.Any(group =>
                group.Count() != 2 ||
                group.Count(voucher =>
                    voucher.Direction == Domain.Enums.CashDirection.Payment) != 1 ||
                group.Count(voucher =>
                    voucher.Direction == Domain.Enums.CashDirection.Receipt) != 1))
        {
            return invalidLinkedTransferError;
        }
        var partnerOpeningBalances = await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.ExchangeRateId == exchangeRateId)
            .ToListAsync(cancellationToken);
        var employeeOpeningBalances = await dbContext.EmployeeOpeningBalances
            .IgnoreQueryFilters()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.ExchangeRateId == exchangeRateId)
            .ToListAsync(cancellationToken);
        var cashboxes = await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.OpeningExchangeRateId == exchangeRateId)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            invoice.ApplyExchangeRate(exchangeRateId, roundedRate);
        }

        foreach (var voucher in linkedVouchers.Where(voucher =>
                     !voucher.CashboxTransferId.HasValue))
        {
            voucher.ApplyExchangeRate(exchangeRateId, roundedRate);
        }

        foreach (var transferVoucherGroup in transferVoucherGroups)
        {
            var paymentVoucher = transferVoucherGroup.Single(voucher =>
                voucher.Direction == Domain.Enums.CashDirection.Payment);
            var receiptVoucher = transferVoucherGroup.Single(voucher =>
                voucher.Direction == Domain.Enums.CashDirection.Receipt);
            var paymentRate = paymentVoucher.ExchangeRateId == exchangeRateId
                ? roundedRate
                : paymentVoucher.ExchangeRate;
            var receiptRate = receiptVoucher.ExchangeRateId == exchangeRateId
                ? roundedRate
                : receiptVoucher.ExchangeRate;

            paymentVoucher.ApplyExchangeRate(
                paymentVoucher.ExchangeRateId,
                paymentRate);
            receiptVoucher.Amount = paymentVoucher.Currency ==
                receiptVoucher.Currency
                    ? paymentVoucher.Amount
                    : Domain.Entities.Companies.ExchangeRateRules.ConvertFromBase(
                        paymentVoucher.BaseAmount,
                        receiptRate);
            receiptVoucher.ApplyExchangeRate(
                receiptVoucher.ExchangeRateId,
                receiptRate);
        }

        foreach (var balance in partnerOpeningBalances)
        {
            balance.ApplyExchangeRate(exchangeRateId, roundedRate);
        }

        foreach (var balance in employeeOpeningBalances)
        {
            balance.ApplyExchangeRate(exchangeRateId, roundedRate);
        }

        foreach (var linkedCashbox in cashboxes)
        {
            linkedCashbox.ApplyOpeningExchangeRate(
                linkedCashbox.OpeningBalanceDate,
                exchangeRateId,
                roundedRate);
        }

        var invoiceIds = invoices.Select(invoice => invoice.Id).ToArray();
        var affectedVouchers = linkedVouchers
            .Concat(transferVouchers)
            .DistinctBy(voucher => voucher.Id)
            .ToArray();
        var voucherIds = affectedVouchers
            .Select(voucher => voucher.Id)
            .ToArray();

        var invoicePayments = await dbContext.InvoicePayments
            .IgnoreQueryFilters()
            .Include(payment => payment.Invoice)
            .Include(payment => payment.CashVoucher)
            .Where(payment =>
                payment.CompanyId == companyId &&
                (invoiceIds.Contains(payment.InvoiceId) ||
                 voucherIds.Contains(payment.CashVoucherId)))
            .ToListAsync(cancellationToken);
        foreach (var payment in invoicePayments)
        {
            payment.Apply(
                invoiceCurrency: payment.InvoiceCurrency,
                appliedAmount: payment.AppliedAmount,
                cashboxCurrency: payment.CashboxCurrency,
                cashboxAmount: payment.CashboxAmount,
                invoiceToBaseRate: payment.Invoice.ExchangeRate,
                cashboxToBaseRate: payment.CashVoucher.ExchangeRate);
        }

        var affectedInvoiceRates = invoices.ToDictionary(
            invoice => invoice.Id,
            invoice => invoice.ExchangeRate);
        var invoicePaymentRates = invoicePayments.ToDictionary(
            payment => payment.CashVoucherId,
            payment => payment.Invoice.ExchangeRate);
        var affectedVoucherRates = affectedVouchers.ToDictionary(
            voucher => voucher.Id,
            voucher => voucher.ExchangeRate);
        var invoicePaymentVoucherIds = invoicePaymentRates.Keys.ToArray();

        var partnerMovements = await dbContext.BusinessPartnerMovements
            .IgnoreQueryFilters()
            .Where(movement =>
                movement.CompanyId == companyId &&
                ((movement.InvoiceId.HasValue &&
                  invoiceIds.Contains(movement.InvoiceId.Value)) ||
                 (movement.CashVoucherId.HasValue &&
                  (voucherIds.Contains(movement.CashVoucherId.Value) ||
                   invoicePaymentVoucherIds.Contains(
                       movement.CashVoucherId.Value)))))
            .ToListAsync(cancellationToken);
        foreach (var movement in partnerMovements)
        {
            if (movement.InvoiceId is int invoiceId &&
                affectedInvoiceRates.TryGetValue(
                    invoiceId,
                    out var invoiceRate))
            {
                movement.ApplyExchangeRate(invoiceRate);
                continue;
            }

            if (movement.CashVoucherId is not int voucherId)
            {
                continue;
            }

            if (invoicePaymentRates.TryGetValue(
                    voucherId,
                    out var paymentInvoiceRate))
            {
                movement.ApplyExchangeRate(paymentInvoiceRate);
                continue;
            }

            if (affectedVoucherRates.TryGetValue(
                    voucherId,
                    out var voucherRate))
            {
                movement.ApplyExchangeRate(voucherRate);
            }
        }

        var employeeMovements = await dbContext.EmployeeMovements
            .IgnoreQueryFilters()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId.HasValue &&
                voucherIds.Contains(movement.CashVoucherId.Value))
            .ToListAsync(cancellationToken);
        foreach (var movement in employeeMovements)
        {
            movement.ApplyExchangeRate(
                affectedVoucherRates[movement.CashVoucherId!.Value]);
        }

        rate.Rate = roundedRate;
        rate.Source = Domain.Enums.ExchangeRateSource.Manual;
        rate.Provider = null;
        rate.Touch(timeProvider.GetUtcNow().UtcDateTime);
        dbContext.Entry(rate)
            .Property(entity => entity.LastModifiedAt)
            .IsModified = true;

        return null;
    }
}

