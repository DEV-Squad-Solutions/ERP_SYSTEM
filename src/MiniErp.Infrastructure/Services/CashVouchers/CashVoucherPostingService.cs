using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed class CashVoucherPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : ICashVoucherPostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        CancellationToken cancellationToken = default)
    {
        if (!voucher.IsPosted)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                PostingAccountRequired());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= voucher.VoucherDate &&
                year.EndDate >= voucher.VoucherDate)
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
                    voucher.VoucherDate,
                    nameof(CashVoucher.VoucherDate)));
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                Closed(
                    voucher.VoucherDate,
                    fiscalYear.Name,
                    nameof(CashVoucher.VoucherDate)));
        }

        if (!voucher.CashboxId.HasValue)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                PostingAccountRequired());
        }

        var cashboxAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.Cashbox,
            voucher.CashboxId.Value,
            cancellationToken);
        if (cashboxAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                cashboxAccountResult.Errors);
        }

        var counterpartResult = await ResolveCounterpartAccountAsync(
            voucher,
            fiscalYear.Id,
            cancellationToken);
        if (counterpartResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                counterpartResult.Errors);
        }

        var amount = voucher.BaseAmount > 0m
            ? voucher.BaseAmount
            : voucher.Amount * voucher.ExchangeRate;
        var isReceipt = voucher.Direction == CashDirection.Receipt;
        var lines = new List<JournalEntryLineRequest>
        {
            new(
                AccountId: cashboxAccountResult.Value,
                Description: voucher.Description,
                Debit: isReceipt ? amount : 0m,
                Credit: isReceipt ? 0m : amount),
            new(
                AccountId: counterpartResult.Value,
                Description: voucher.Description,
                Debit: isReceipt ? 0m : amount,
                Credit: isReceipt ? amount : 0m)
        };

        return await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYear.Id,
                EntryDate: voucher.VoucherDate,
                Description: BuildDescription(voucher),
                SourceType: JournalEntrySourceType.CashVoucher,
                SourceId: voucher.Id,
                SourceNumber: voucher.VoucherNumber,
                Lines: lines),
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        int voucherId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            voucherId,
            cancellationToken);

    private async Task<Result<int>> ResolveCounterpartAccountAsync(
        CashVoucher voucher,
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        if (voucher.AccountId.HasValue)
        {
            return Result<int>.Success(voucher.AccountId.Value);
        }

        var mappingType = voucher.PartyType switch
        {
            CashPartyType.Partner when
                voucher.Direction == CashDirection.Receipt =>
                AccountingMappingType.CustomerControl,
            CashPartyType.Partner => AccountingMappingType.SupplierControl,
            CashPartyType.Driver => AccountingMappingType.DriverControl,
            CashPartyType.Employee => AccountingMappingType.EmployeeControl,
            CashPartyType.Other or CashPartyType.None when
                voucher.CashMovementTypeId.HasValue =>
                AccountingMappingType.CashMovementType,
            _ => (AccountingMappingType?)null
        };
        if (!mappingType.HasValue)
        {
            return Result<int>.Failure(PostingAccountRequired());
        }

        var sourceId = mappingType == AccountingMappingType.CashMovementType
            ? voucher.CashMovementTypeId
            : null;
        return await accountMappingResolver.ResolveAsync(
            fiscalYearId,
            mappingType.Value,
            sourceId,
            cancellationToken);
    }

    private static string BuildDescription(CashVoucher voucher) =>
        $"{(voucher.Direction == CashDirection.Receipt ? "سند قبض" : "سند صرف")} " +
        $"{voucher.VoucherNumber}" +
        (string.IsNullOrWhiteSpace(voucher.Description)
            ? string.Empty
            : $" - {voucher.Description.Trim()}");
}
