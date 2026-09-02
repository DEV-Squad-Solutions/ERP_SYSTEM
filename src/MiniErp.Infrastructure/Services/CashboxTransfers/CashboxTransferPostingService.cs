using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.CashboxTransfers.CashboxTransferErrors;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;

namespace MiniErp.Infrastructure.Services.CashboxTransfers;

public sealed class CashboxTransferPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : ICashboxTransferPostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        int transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == transferId)
            .Select(entity => new
            {
                entity.Id,
                entity.TransferNumber,
                entity.TransferDate,
                entity.SourceCashboxId,
                entity.DestinationCashboxId,
                entity.Description
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (transfer is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                CashboxTransferErrors.NotFound(transferId));
        }

        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.CashboxTransferId == transferId &&
                voucher.IsPosted)
            .Select(voucher => new
            {
                voucher.Direction,
                voucher.CashboxId,
                voucher.Amount,
                voucher.ExchangeRate,
                voucher.BaseAmount
            })
            .ToListAsync(cancellationToken);
        var payment = vouchers.SingleOrDefault(voucher =>
            voucher.Direction == CashDirection.Payment &&
            voucher.CashboxId == transfer.SourceCashboxId);
        var receipt = vouchers.SingleOrDefault(voucher =>
            voucher.Direction == CashDirection.Receipt &&
            voucher.CashboxId == transfer.DestinationCashboxId);
        if (vouchers.Count != 2 || payment is null || receipt is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                InvalidVoucherPair());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= transfer.TransferDate &&
                year.EndDate >= transfer.TransferDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                DateNotCovered(
                    transfer.TransferDate,
                    "TransferDate"));
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                Closed(
                    transfer.TransferDate,
                    fiscalYear.Name,
                    "TransferDate"));
        }

        var sourceAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.Cashbox,
            transfer.SourceCashboxId,
            cancellationToken);
        if (sourceAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                sourceAccountResult.Errors);
        }

        var destinationAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.Cashbox,
            transfer.DestinationCashboxId,
            cancellationToken);
        if (destinationAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                destinationAccountResult.Errors);
        }

        var sourceBaseAmount = GetBaseAmount(
            payment.BaseAmount,
            payment.Amount,
            payment.ExchangeRate);
        var destinationBaseAmount = GetBaseAmount(
            receipt.BaseAmount,
            receipt.Amount,
            receipt.ExchangeRate);
        var lines = new List<JournalEntryLineRequest>
        {
            new(
                AccountId: destinationAccountResult.Value,
                Description: transfer.Description,
                Debit: destinationBaseAmount,
                Credit: 0m),
            new(
                AccountId: sourceAccountResult.Value,
                Description: transfer.Description,
                Debit: 0m,
                Credit: sourceBaseAmount)
        };

        var difference = ExchangeRateRules.RoundBaseAmount(
            destinationBaseAmount - sourceBaseAmount);
        if (difference > 0m)
        {
            var gainAccountResult = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id,
                AccountingMappingType.ExchangeGain,
                cancellationToken: cancellationToken);
            if (gainAccountResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    gainAccountResult.Errors);
            }

            lines.Add(new JournalEntryLineRequest(
                AccountId: gainAccountResult.Value,
                Description: "فرق عملة ناتج عن تحويل الخزائن",
                Debit: 0m,
                Credit: difference));
        }
        else if (difference < 0m)
        {
            var lossAccountResult = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id,
                AccountingMappingType.ExchangeLoss,
                cancellationToken: cancellationToken);
            if (lossAccountResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    lossAccountResult.Errors);
            }

            lines.Add(new JournalEntryLineRequest(
                AccountId: lossAccountResult.Value,
                Description: "فرق عملة ناتج عن تحويل الخزائن",
                Debit: Math.Abs(difference),
                Credit: 0m));
        }

        return await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYear.Id,
                EntryDate: transfer.TransferDate,
                Description: $"تحويل خزائن {transfer.TransferNumber}" +
                    (string.IsNullOrWhiteSpace(transfer.Description)
                        ? string.Empty
                        : $" - {transfer.Description}"),
                SourceType: JournalEntrySourceType.CashboxTransfer,
                SourceId: transfer.Id,
                SourceNumber: transfer.TransferNumber,
                Lines: lines),
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        int transferId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            JournalEntrySourceType.CashboxTransfer,
            transferId,
            cancellationToken);

    private static decimal GetBaseAmount(
        decimal baseAmount,
        decimal amount,
        decimal exchangeRate) =>
        baseAmount > 0m
            ? baseAmount
            : ExchangeRateRules.ConvertToBase(amount, exchangeRate);
}
