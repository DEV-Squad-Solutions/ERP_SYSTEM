using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;
using static MiniErp.Application.Features.CashboxTransfers.CashboxTransferErrors;

namespace MiniErp.Infrastructure.Services.CashboxTransfers;

public sealed partial class CashboxTransferService
{
    private async Task<Result<TransferPreparation>> PrepareAsync(
        DateOnly transferDate,
        int sourceCashboxId,
        int destinationCashboxId,
        decimal sourceAmount,
        decimal? requestedExchangeRate,
        decimal? requestedDestinationAmount,
        decimal? requestedConversionRate,
        decimal? requestedDestinationExchangeRate,
        CashboxTransfer? currentTransfer,
        CancellationToken cancellationToken)
    {
        if (sourceCashboxId == destinationCashboxId)
        {
            return Result<TransferPreparation>.Failure(
                CashboxesMustDiffer());
        }

        foreach (var cashboxId in new[] { sourceCashboxId, destinationCashboxId })
        {
            var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, cashboxId, cancellationToken);
            if (access.IsFailure) return Result<TransferPreparation>.Failure(access.Errors);
        }

        var cashboxes = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                (cashbox.Id == sourceCashboxId ||
                 cashbox.Id == destinationCashboxId))
            .ToListAsync(cancellationToken);
        var sourceCashbox = cashboxes.SingleOrDefault(
            cashbox => cashbox.Id == sourceCashboxId);
        if (sourceCashbox is null)
        {
            return Result<TransferPreparation>.Failure(
                CashboxNotFound(
                    sourceCashboxId,
                    nameof(CashboxTransferRequest.SourceCashboxId)));
        }

        var destinationCashbox = cashboxes.SingleOrDefault(
            cashbox => cashbox.Id == destinationCashboxId);
        if (destinationCashbox is null)
        {
            return Result<TransferPreparation>.Failure(
                CashboxNotFound(
                    destinationCashboxId,
                    nameof(CashboxTransferRequest.DestinationCashboxId)));
        }

        var existingCashboxIds = currentTransfer is null
            ? []
            : new HashSet<int>
            {
                currentTransfer.SourceCashboxId,
                currentTransfer.DestinationCashboxId
            };
        if (!sourceCashbox.IsActive &&
            !existingCashboxIds.Contains(sourceCashbox.Id))
        {
            return Result<TransferPreparation>.Failure(
                CashboxInactive(
                    sourceCashbox.Id,
                    nameof(CashboxTransferRequest.SourceCashboxId)));
        }

        if (!destinationCashbox.IsActive &&
            !existingCashboxIds.Contains(destinationCashbox.Id))
        {
            return Result<TransferPreparation>.Failure(
                CashboxInactive(
                    destinationCashbox.Id,
                    nameof(CashboxTransferRequest.DestinationCashboxId)));
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var sourceRateRequest = requestedExchangeRate ??
            (destinationCashbox.Currency == baseCurrency
                ? requestedConversionRate
                : null);
        var sourceExchangeRateResult = await exchangeRateResolver.ResolveAsync(
            sourceCashbox.Currency,
            transferDate,
            sourceRateRequest,
            cancellationToken);
        if (sourceExchangeRateResult.IsFailure)
        {
            return Result<TransferPreparation>.Failure(
                sourceExchangeRateResult.Error);
        }

        var sourceExchangeRate = sourceExchangeRateResult.Value;
        var sourceBaseAmount = ExchangeRateRules.ConvertToBase(
            sourceAmount,
            sourceExchangeRate.Rate);

        if (sourceCashbox.Currency == destinationCashbox.Currency)
        {
            if (requestedDestinationExchangeRate.HasValue &&
                requestedDestinationExchangeRate.Value !=
                sourceExchangeRate.Rate)
            {
                return Result<TransferPreparation>.Failure(
                    InvalidRequest());
            }

            if (requestedConversionRate.HasValue &&
                requestedConversionRate.Value != 1m)
            {
                return Result<TransferPreparation>.Failure(InvalidRequest());
            }

            if (requestedDestinationAmount.HasValue &&
                requestedDestinationAmount.Value != sourceAmount)
            {
                return Result<TransferPreparation>.Failure(
                    DestinationAmountMustMatchSourceAmount());
            }

            return Result<TransferPreparation>.Success(
                new TransferPreparation(
                    SourceCashbox: sourceCashbox,
                    DestinationCashbox: destinationCashbox,
                    DestinationAmount: sourceAmount,
                    SourceExchangeRate: sourceExchangeRate,
                    DestinationExchangeRate: sourceExchangeRate));
        }

        ResolvedExchangeRate destinationExchangeRate;
        decimal destinationAmount;
        var hasManualDestinationAmount =
            requestedDestinationAmount.HasValue ||
            requestedConversionRate.HasValue;

        if (destinationCashbox.Currency == baseCurrency)
        {
            var destinationExchangeRateResult =
                await exchangeRateResolver.ResolveAsync(
                    destinationCashbox.Currency,
                    transferDate,
                    requestedDestinationExchangeRate,
                    cancellationToken);
            if (destinationExchangeRateResult.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    destinationExchangeRateResult.Error);
            }

            destinationExchangeRate = destinationExchangeRateResult.Value;
            destinationAmount = decimal.Round(
                sourceBaseAmount,
                2,
                MidpointRounding.AwayFromZero);

            var calculatedFromConversionRate =
                CalculateDestinationAmountFromConversionRate(
                    sourceAmount,
                    requestedConversionRate);
            if (calculatedFromConversionRate.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    calculatedFromConversionRate.Error);
            }

            if (requestedDestinationAmount.HasValue &&
                calculatedFromConversionRate.Value.HasValue &&
                requestedDestinationAmount.Value !=
                calculatedFromConversionRate.Value.Value)
            {
                return Result<TransferPreparation>.Failure(
                    ConversionRateDoesNotMatchDestinationAmount());
            }

            if (requestedDestinationAmount.HasValue &&
                requestedDestinationAmount.Value != destinationAmount)
            {
                return Result<TransferPreparation>.Failure(
                    DestinationAmountDoesNotMatchBaseAmount());
            }

            if (calculatedFromConversionRate.Value.HasValue &&
                calculatedFromConversionRate.Value.Value != destinationAmount)
            {
                return Result<TransferPreparation>.Failure(
                    DestinationAmountDoesNotMatchBaseAmount());
            }
        }
        else if (hasManualDestinationAmount &&
                 !requestedDestinationExchangeRate.HasValue)
        {
            var calculatedFromConversionRate =
                CalculateDestinationAmountFromConversionRate(
                    sourceAmount,
                    requestedConversionRate);
            if (calculatedFromConversionRate.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    calculatedFromConversionRate.Error);
            }

            if (requestedDestinationAmount.HasValue &&
                calculatedFromConversionRate.Value.HasValue &&
                requestedDestinationAmount.Value !=
                calculatedFromConversionRate.Value.Value)
            {
                return Result<TransferPreparation>.Failure(
                    ConversionRateDoesNotMatchDestinationAmount());
            }

            destinationAmount = calculatedFromConversionRate.Value ??
                requestedDestinationAmount!.Value;
            if (destinationAmount <= 0m)
            {
                return Result<TransferPreparation>.Failure(
                    DestinationAmountRequired());
            }

            var manuallyImpliedRate = ExchangeRateRules.RoundRate(
                sourceBaseAmount / destinationAmount);
            if (!ExchangeRateRules.IsValidRate(manuallyImpliedRate))
            {
                return Result<TransferPreparation>.Failure(InvalidRequest());
            }

            var destinationExchangeRateResult =
                await exchangeRateResolver.ResolveAsync(
                    destinationCashbox.Currency,
                    transferDate,
                    manuallyImpliedRate,
                    cancellationToken);
            if (destinationExchangeRateResult.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    destinationExchangeRateResult.Error);
            }

            destinationExchangeRate = destinationExchangeRateResult.Value;
        }
        else
        {
            var destinationExchangeRateResult =
                await exchangeRateResolver.ResolveAsync(
                    destinationCashbox.Currency,
                    transferDate,
                    requestedDestinationExchangeRate,
                    cancellationToken);
            if (destinationExchangeRateResult.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    destinationExchangeRateResult.Error);
            }

            destinationExchangeRate = destinationExchangeRateResult.Value;
            try
            {
                destinationAmount = ExchangeRateRules.ConvertFromBase(
                    sourceBaseAmount,
                    destinationExchangeRate.Rate);
            }
            catch (OverflowException)
            {
                return Result<TransferPreparation>.Failure(InvalidRequest());
            }
        }

        if (requestedDestinationExchangeRate.HasValue &&
            hasManualDestinationAmount)
        {
            var calculatedFromConversionRate =
                CalculateDestinationAmountFromConversionRate(
                    sourceAmount,
                    requestedConversionRate);
            if (calculatedFromConversionRate.IsFailure)
            {
                return Result<TransferPreparation>.Failure(
                    calculatedFromConversionRate.Error);
            }

            if (requestedDestinationAmount.HasValue &&
                calculatedFromConversionRate.Value.HasValue &&
                requestedDestinationAmount.Value !=
                calculatedFromConversionRate.Value.Value)
            {
                return Result<TransferPreparation>.Failure(
                    ConversionRateDoesNotMatchDestinationAmount());
            }

            var manualDestinationAmount =
                calculatedFromConversionRate.Value ??
                requestedDestinationAmount!.Value;
            if (manualDestinationAmount != destinationAmount)
            {
                return Result<TransferPreparation>.Failure(
                    DestinationAmountDoesNotMatchExchangeRates());
            }
        }

        return Result<TransferPreparation>.Success(
            new TransferPreparation(
                SourceCashbox: sourceCashbox,
                DestinationCashbox: destinationCashbox,
                DestinationAmount: destinationAmount,
                SourceExchangeRate: sourceExchangeRate,
                DestinationExchangeRate: destinationExchangeRate));
    }

    private static Result<decimal?>
        CalculateDestinationAmountFromConversionRate(
            decimal sourceAmount,
            decimal? conversionRate)
    {
        if (!conversionRate.HasValue)
        {
            return Result<decimal?>.Success(null);
        }

        try
        {
            return Result<decimal?>.Success(decimal.Round(
                sourceAmount * conversionRate.Value,
                2,
                MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            return Result<decimal?>.Failure(InvalidRequest());
        }
    }

    private CashVoucher CreateVoucher(
        CashboxTransfer transfer,
        Cashbox cashbox,
        CashDirection direction,
        string voucherNumber,
        decimal amount,
        ResolvedExchangeRate exchangeRate,
        DateTime now)
    {
        var voucher = new CashVoucher();
        ApplyVoucher(
            voucher,
            transfer,
            cashbox,
            direction,
            voucherNumber,
            amount,
            exchangeRate,
            now);
        return voucher;
    }

    private void ApplyVoucher(
        CashVoucher voucher,
        CashboxTransfer transfer,
        Cashbox cashbox,
        CashDirection direction,
        string voucherNumber,
        decimal amount,
        ResolvedExchangeRate exchangeRate,
        DateTime now)
    {
        voucher.CompanyId = companyId;
        voucher.CashboxTransferId = transfer.Id;
        voucher.CashboxTransfer = transfer;
        voucher.InvoiceId = null;
        voucher.VoucherNumber = voucherNumber;
        voucher.VoucherDate = transfer.TransferDate;
        voucher.Direction = direction;
        voucher.CashboxId = cashbox.Id;
        voucher.CashMovementTypeId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.BusinessPartnerId = null;
        voucher.DriverId = null;
        voucher.DriverTripId = null;
        voucher.ExternalPartyName = null;
        voucher.Amount = amount;
        voucher.Currency = cashbox.Currency;
        voucher.IsPosted = true;
        voucher.ReferenceNumber = transfer.TransferNumber;
        voucher.Description = transfer.Description ??
            $"Cashbox transfer {transfer.TransferNumber}";
        voucher.Notes = transfer.Notes;
        voucher.ApplyExchangeRate(
            exchangeRate.ExchangeRateId,
            exchangeRate.Rate);
        voucher.Touch(now);
    }

    private async Task<CashboxTransferResponse?> BuildResponseAsync(
        int id,
        CancellationToken cancellationToken,
        int? fiscalYearId = null)
    {
        var transfer = await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == id &&
                (!fiscalYearId.HasValue ||
                 entity.FiscalYearId == fiscalYearId.Value))
            .Select(entity => new
            {
                entity.Id,
                entity.CompanyId,
                entity.TransferNumber,
                entity.TransferDate,
                entity.FiscalYearId,
                FiscalYearName = entity.FiscalYear.Name,
                entity.SourceCashboxId,
                SourceCashboxName = entity.SourceCashbox.Name,
                entity.DestinationCashboxId,
                DestinationCashboxName = entity.DestinationCashbox.Name,
                Currency = entity.SourceCashbox.Currency,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency,
                entity.Description,
                entity.Notes,
                entity.LastModifiedAt,
                entity.RowVersion
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (transfer is null)
        {
            return null;
        }

        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.CashboxTransferId == id &&
                voucher.FiscalYearId == transfer.FiscalYearId)
            .Select(voucher => new
            {
                voucher.Id,
                voucher.VoucherNumber,
                voucher.Direction,
                voucher.Amount,
                voucher.Currency,
                voucher.ExchangeRate,
                voucher.BaseAmount
            })
            .ToListAsync(cancellationToken);
        var paymentVoucher = vouchers.FirstOrDefault(
            voucher => voucher.Direction == CashDirection.Payment);
        var receiptVoucher = vouchers.FirstOrDefault(
            voucher => voucher.Direction == CashDirection.Receipt);
        if (vouchers.Count != 2 ||
            vouchers.Count(voucher =>
                voucher.Direction == CashDirection.Payment) != 1 ||
            vouchers.Count(voucher =>
                voucher.Direction == CashDirection.Receipt) != 1 ||
            paymentVoucher is null ||
            receiptVoucher is null)
        {
            return null;
        }

        return new CashboxTransferResponse(
            Id: transfer.Id,
            CompanyId: transfer.CompanyId,
            TransferNumber: transfer.TransferNumber,
            TransferDate: transfer.TransferDate,
            SourceCashboxId: transfer.SourceCashboxId,
            SourceCashboxName: transfer.SourceCashboxName,
            DestinationCashboxId: transfer.DestinationCashboxId,
            DestinationCashboxName: transfer.DestinationCashboxName,
            Amount: paymentVoucher.Amount,
            Currency: transfer.Currency,
            BaseCurrency: transfer.BaseCurrency,
            ExchangeRate: paymentVoucher.ExchangeRate,
            BaseAmount: paymentVoucher.BaseAmount,
            PaymentVoucherId: paymentVoucher.Id,
            PaymentVoucherNumber: paymentVoucher.VoucherNumber,
            ReceiptVoucherId: receiptVoucher.Id,
            ReceiptVoucherNumber: receiptVoucher.VoucherNumber,
            Description: transfer.Description,
            Notes: transfer.Notes,
            LastModifiedAt: transfer.LastModifiedAt,
            RowVersion: transfer.RowVersion)
        {
            FiscalYearId = transfer.FiscalYearId,
            FiscalYearName = transfer.FiscalYearName,
            DestinationAmount = receiptVoucher.Amount,
            DestinationCurrency = receiptVoucher.Currency,
            DestinationExchangeRate = receiptVoucher.ExchangeRate,
            DestinationBaseAmount = receiptVoucher.BaseAmount,
            ConversionRate = ExchangeRateRules.RoundRate(
                receiptVoucher.Amount / paymentVoucher.Amount)
        };
    }

    private static bool TryGetVoucherPair(
        IEnumerable<CashVoucher> vouchers,
        out CashVoucher paymentVoucher,
        out CashVoucher receiptVoucher)
    {
        var voucherList = vouchers.ToList();
        paymentVoucher = voucherList.FirstOrDefault(voucher =>
            voucher.Direction == CashDirection.Payment)!;
        receiptVoucher = voucherList.FirstOrDefault(voucher =>
            voucher.Direction == CashDirection.Receipt)!;
        return voucherList.Count == 2 &&
            voucherList.Count(voucher =>
                voucher.Direction == CashDirection.Payment) == 1 &&
            voucherList.Count(voucher =>
                voucher.Direction == CashDirection.Receipt) == 1 &&
            paymentVoucher is not null &&
            receiptVoucher is not null;
    }

    private static Error? ValidateRequestShape(
        DateOnly transferDate,
        int sourceCashboxId,
        int destinationCashboxId,
        decimal amount,
        string? description,
        string? notes,
        decimal? exchangeRate,
        decimal? destinationAmount,
        decimal? conversionRate,
        decimal? destinationExchangeRate)
    {
        if (sourceCashboxId == destinationCashboxId)
        {
            return CashboxesMustDiffer();
        }

        if (transferDate == default ||
            sourceCashboxId <= 0 ||
            destinationCashboxId <= 0 ||
            amount <= 0m ||
            decimal.Round(amount, 2) != amount ||
            (destinationAmount.HasValue &&
             (destinationAmount.Value <= 0m ||
              decimal.Round(destinationAmount.Value, 2) !=
              destinationAmount.Value)) ||
            description?.Length >
                CashboxTransferRequest.DescriptionMaximumLength ||
            notes?.Length > CashboxTransferRequest.NotesMaximumLength ||
            (exchangeRate.HasValue &&
             !ExchangeRateRules.IsValidRate(exchangeRate.Value)) ||
            (conversionRate.HasValue &&
             !ExchangeRateRules.IsValidRate(conversionRate.Value)) ||
            (destinationExchangeRate.HasValue &&
             !ExchangeRateRules.IsValidRate(destinationExchangeRate.Value)))
        {
            return InvalidRequest();
        }

        return null;
    }

    private static Error? ValidateFilters(
        CashboxTransferFilterRequest filters)
    {
        if (filters.Search?.Trim().Length > 100 ||
            filters.FiscalYearId is <= 0 ||
            filters.SourceCashboxId is <= 0 ||
            filters.DestinationCashboxId is <= 0 ||
            filters.ToDate < filters.FromDate)
        {
            return FiltersInvalid();
        }

        return null;
    }

    private Task<string> GenerateIdentifierAsync(
        string prefix,
        IQueryable<string> existingIdentifiers,
        CancellationToken cancellationToken) =>
        EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix,
            companyId,
            existingIdentifiers,
            cancellationToken);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TransferPreparation(
        Cashbox SourceCashbox,
        Cashbox DestinationCashbox,
        decimal DestinationAmount,
        ResolvedExchangeRate SourceExchangeRate,
        ResolvedExchangeRate DestinationExchangeRate);
}
