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
using static MiniErp.Application.Features.CashboxTransfers.CashboxTransferErrors;

namespace MiniErp.Infrastructure.Services.CashboxTransfers;

public sealed class CashboxTransferService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider)
    : ICashboxTransferService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashboxTransferListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            CashboxTransferFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        if (ValidateFilters(filters) is { } filterError)
        {
            return Result<PagedResponse<CashboxTransferListResponse>>.Failure(
                filterError);
        }

        var search = filters.Search?.Trim();
        var query = dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer => transfer.CompanyId == companyId)
            .Where(transfer =>
                string.IsNullOrEmpty(search) ||
                transfer.TransferNumber.Contains(search) ||
                transfer.SourceCashbox.Name.Contains(search) ||
                transfer.DestinationCashbox.Name.Contains(search) ||
                (transfer.Description != null &&
                 transfer.Description.Contains(search)))
            .Where(transfer =>
                !filters.SourceCashboxId.HasValue ||
                transfer.SourceCashboxId == filters.SourceCashboxId.Value)
            .Where(transfer =>
                !filters.DestinationCashboxId.HasValue ||
                transfer.DestinationCashboxId ==
                    filters.DestinationCashboxId.Value)
            .Where(transfer =>
                !filters.FromDate.HasValue ||
                transfer.TransferDate >= filters.FromDate.Value)
            .Where(transfer =>
                !filters.ToDate.HasValue ||
                transfer.TransferDate <= filters.ToDate.Value)
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.Id);

        return await paginationService.PaginateAsync<
            CashboxTransfer,
            CashboxTransferListResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<CashboxTransferResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxTransferResponse>.Failure(InvalidId());
        }

        var response = await BuildResponseAsync(id, cancellationToken);
        return response is null
            ? Result<CashboxTransferResponse>.Failure(NotFound(id))
            : Result<CashboxTransferResponse>.Success(response);
    }

    public async Task<Result<CashboxTransferResponse>> AddAsync(
        CashboxTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequestShape(
                request.TransferDate,
                request.SourceCashboxId,
                request.DestinationCashboxId,
                request.Amount,
                request.Description,
                request.Notes,
                request.ExchangeRate,
                request.DestinationAmount,
                request.ConversionRate,
                request.DestinationExchangeRate) is { } shapeError)
        {
            return Result<CashboxTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var preparation = await PrepareAsync(
            request.TransferDate,
            request.SourceCashboxId,
            request.DestinationCashboxId,
            request.Amount,
            request.ExchangeRate,
            request.DestinationAmount,
            request.ConversionRate,
            request.DestinationExchangeRate,
            currentTransfer: null,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(
                preparation.Error);
        }

        var balanceError = await ValidateFinalBalancesAsync(
            excludedVouchers: [],
            proposals:
            [
                new CashboxBalanceProposal(
                    CashboxId: request.SourceCashboxId,
                    Direction: CashDirection.Payment,
                    Amount: request.Amount),
                new CashboxBalanceProposal(
                    CashboxId: request.DestinationCashboxId,
                    Direction: CashDirection.Receipt,
                    Amount: preparation.Value.DestinationAmount)
            ],
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<CashboxTransferResponse>.Failure(balanceError);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transferNumber = await GenerateIdentifierAsync(
            prefix: "TRF",
            dbContext.CashboxTransfers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.TransferNumber),
            cancellationToken);
        var paymentVoucherNumber = await GenerateIdentifierAsync(
            prefix: "PAY",
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
        var receiptVoucherNumber = await GenerateIdentifierAsync(
            prefix: "RCV",
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
        var transfer = new CashboxTransfer
        {
            CompanyId = companyId,
            TransferNumber = transferNumber,
            TransferDate = request.TransferDate,
            SourceCashboxId = request.SourceCashboxId,
            DestinationCashboxId = request.DestinationCashboxId,
            Description = Normalize(request.Description),
            Notes = Normalize(request.Notes)
        };
        transfer.Touch(now);
        dbContext.CashboxTransfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.CashVouchers.AddRange(
            CreateVoucher(
                transfer,
                preparation.Value.SourceCashbox,
                CashDirection.Payment,
                paymentVoucherNumber,
                request.Amount,
                preparation.Value.SourceExchangeRate,
                now),
            CreateVoucher(
                transfer,
                preparation.Value.DestinationCashbox,
                CashDirection.Receipt,
                receiptVoucherNumber,
                preparation.Value.DestinationAmount,
                preparation.Value.DestinationExchangeRate,
                now));
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildResponseAsync(
            transfer.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxTransferResponse>.Success(response!);
    }

    public async Task<Result<CashboxTransferResponse>> UpdateAsync(
        int id,
        CashboxTransferUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxTransferResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashboxTransferResponse>.Failure(
                RowVersionRequired());
        }

        if (ValidateRequestShape(
                request.TransferDate,
                request.SourceCashboxId,
                request.DestinationCashboxId,
                request.Amount,
                request.Description,
                request.Notes,
                request.ExchangeRate,
                request.DestinationAmount,
                request.ConversionRate,
                request.DestinationExchangeRate) is { } shapeError)
        {
            return Result<CashboxTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        var transfer = await dbContext.CashboxTransfers
            .Include(entity => entity.Vouchers)
            .SingleOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (transfer is null)
        {
            return Result<CashboxTransferResponse>.Failure(NotFound(id));
        }

        if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashboxTransferResponse>.Failure(Concurrency());
        }

        if (!TryGetVoucherPair(
                transfer.Vouchers,
                out var paymentVoucher,
                out var receiptVoucher))
        {
            return Result<CashboxTransferResponse>.Failure(
                InvalidVoucherPair());
        }

        var preparation = await PrepareAsync(
            request.TransferDate,
            request.SourceCashboxId,
            request.DestinationCashboxId,
            request.Amount,
            request.ExchangeRate,
            request.DestinationAmount,
            request.ConversionRate,
            request.DestinationExchangeRate,
            transfer,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(
                preparation.Error);
        }

        var balanceError = await ValidateFinalBalancesAsync(
            excludedVouchers: [paymentVoucher, receiptVoucher],
            proposals:
            [
                new CashboxBalanceProposal(
                    CashboxId: request.SourceCashboxId,
                    Direction: CashDirection.Payment,
                    Amount: request.Amount),
                new CashboxBalanceProposal(
                    CashboxId: request.DestinationCashboxId,
                    Direction: CashDirection.Receipt,
                    Amount: preparation.Value.DestinationAmount)
            ],
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<CashboxTransferResponse>.Failure(balanceError);
        }

        var entry = dbContext.Entry(transfer);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        transfer.TransferDate = request.TransferDate;
        transfer.SourceCashboxId = request.SourceCashboxId;
        transfer.DestinationCashboxId = request.DestinationCashboxId;
        transfer.Description = Normalize(request.Description);
        transfer.Notes = Normalize(request.Notes);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        transfer.Touch(now);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        ApplyVoucher(
            paymentVoucher,
            transfer,
            preparation.Value.SourceCashbox,
            CashDirection.Payment,
            paymentVoucher.VoucherNumber,
            request.Amount,
            preparation.Value.SourceExchangeRate,
            now);
        ApplyVoucher(
            receiptVoucher,
            transfer,
            preparation.Value.DestinationCashbox,
            CashDirection.Receipt,
            receiptVoucher.VoucherNumber,
            preparation.Value.DestinationAmount,
            preparation.Value.DestinationExchangeRate,
            now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashboxTransferResponse>.Failure(Concurrency());
        }

        var response = await BuildResponseAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxTransferResponse>.Success(response!);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        var transfer = await dbContext.CashboxTransfers
            .Include(entity => entity.Vouchers)
            .SingleOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (transfer is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (!TryGetVoucherPair(
                transfer.Vouchers,
                out var paymentVoucher,
                out var receiptVoucher))
        {
            return Result.Failure(InvalidVoucherPair());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            excludedVouchers: [paymentVoucher, receiptVoucher],
            proposals: [],
            cancellationToken);
        if (balanceError is not null)
        {
            return Result.Failure(balanceError);
        }

        try
        {
            dbContext.CashVouchers.RemoveRange(
                paymentVoucher,
                receiptVoucher);
            dbContext.CashboxTransfers.Remove(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }
    }

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

    private async Task<Error?> ValidateFinalBalancesAsync(
        IReadOnlyCollection<CashVoucher> excludedVouchers,
        IReadOnlyCollection<CashboxBalanceProposal> proposals,
        CancellationToken cancellationToken)
    {
        var excludedVoucherIds = excludedVouchers
            .Select(voucher => voucher.Id)
            .ToArray();
        var affectedCashboxIds = excludedVouchers
            .Where(voucher => voucher.CashboxId.HasValue)
            .Select(voucher => voucher.CashboxId!.Value)
            .Concat(proposals.Select(proposal => proposal.CashboxId))
            .Distinct()
            .ToArray();

        var balances = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                affectedCashboxIds.Contains(cashbox.Id))
            .Select(cashbox => new
            {
                cashbox.Id,
                Balance = cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.IsPosted &&
                            !excludedVoucherIds.Contains(voucher.Id))
                        .Sum(voucher =>
                            (decimal?)(voucher.Direction ==
                                CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount)) ?? 0m)
            })
            .ToDictionaryAsync(
                cashbox => cashbox.Id,
                cashbox => cashbox.Balance,
                cancellationToken);

        foreach (var proposal in proposals)
        {
            balances[proposal.CashboxId] +=
                proposal.Direction == CashDirection.Receipt
                    ? proposal.Amount
                    : -proposal.Amount;
        }

        foreach (var (cashboxId, balance) in balances)
        {
            if (balance < 0m)
            {
                return InsufficientCashboxBalance(cashboxId);
            }
        }

        return null;
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
        CancellationToken cancellationToken)
    {
        var transfer = await dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == id)
            .Select(entity => new
            {
                entity.Id,
                entity.CompanyId,
                entity.TransferNumber,
                entity.TransferDate,
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
                voucher.CashboxTransferId == id)
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

    private sealed record CashboxBalanceProposal(
        int CashboxId,
        CashDirection Direction,
        decimal Amount);
}
