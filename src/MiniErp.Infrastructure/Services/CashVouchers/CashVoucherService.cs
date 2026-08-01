using System.Data;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed class CashVoucherService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider)
    : ICashVoucherService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashVoucherResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashVoucherFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CashVoucherFilterRequest();
        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();

        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId)
            .Where(voucher =>
                string.IsNullOrEmpty(search) ||
                voucher.VoucherNumber.Contains(search) ||
                (voucher.Cashbox != null &&
                 (voucher.Cashbox.Code.Contains(search) ||
                  voucher.Cashbox.Name.Contains(search))) ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Name.Contains(search)) ||
                (voucher.BusinessPartner != null &&
                 (voucher.BusinessPartner.Code.Contains(search) ||
                  voucher.BusinessPartner.Name.Contains(search))) ||
                (voucher.Driver != null &&
                 (voucher.Driver.Code.Contains(search) ||
                  voucher.Driver.Name.Contains(search))) ||
                (voucher.DriverTrip != null &&
                 voucher.DriverTrip.InvoiceNumber.Contains(search)) ||
                (voucher.Invoice != null &&
                 (voucher.Invoice.InvoiceNumber.Contains(search) ||
                  (voucher.Invoice.PartnerInvoiceNo != null &&
                   voucher.Invoice.PartnerInvoiceNo.Contains(search)))) ||
                (voucher.ExternalPartyName != null &&
                 voucher.ExternalPartyName.Contains(search)) ||
                (voucher.ReferenceNumber != null &&
                 voucher.ReferenceNumber.Contains(search)) ||
                (voucher.Description != null &&
                 voucher.Description.Contains(search)))
            .Where(voucher =>
                string.IsNullOrEmpty(voucherNumber) ||
                voucher.VoucherNumber.Contains(voucherNumber))
            .Where(voucher =>
                !filters.Direction.HasValue ||
                voucher.Direction == filters.Direction.Value)
            .Where(voucher =>
                !filters.CashboxId.HasValue ||
                voucher.CashboxId == filters.CashboxId.Value)
            .Where(voucher =>
                !filters.CashMovementTypeId.HasValue ||
                voucher.CashMovementTypeId ==
                filters.CashMovementTypeId.Value)
            .Where(voucher =>
                !filters.PartyType.HasValue ||
                voucher.PartyType == filters.PartyType.Value)
            .Where(voucher =>
                !filters.BusinessPartnerId.HasValue ||
                voucher.BusinessPartnerId ==
                filters.BusinessPartnerId.Value)
            .Where(voucher =>
                !filters.DriverId.HasValue ||
                voucher.DriverId == filters.DriverId.Value)
            .Where(voucher =>
                !filters.DriverTripId.HasValue ||
                voucher.DriverTripId == filters.DriverTripId.Value)
            .Where(voucher =>
                !filters.FromDate.HasValue ||
                voucher.VoucherDate >= filters.FromDate.Value)
            .Where(voucher =>
                !filters.ToDate.HasValue ||
                voucher.VoucherDate <= filters.ToDate.Value)
            .OrderByDescending(voucher => voucher.VoucherDate)
            .ThenByDescending(voucher => voucher.Id);

        return await paginationService.PaginateAsync<
            CashVoucher,
            CashVoucherResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<CashVoucherResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CashVoucherResponse>.Failure(NotFound(id))
            : Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherResponse>> AddAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var preparation = await PrepareAsync(
            request,
            currentVoucher: null,
            cancellationToken);
        if (preparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(preparation.Error);
        }

        var voucher = request.Adapt<CashVoucher>();
        voucher.CompanyId = companyId;
        voucher.VoucherNumber = string.IsNullOrWhiteSpace(
            request.VoucherNumber)
            ? GenerateVoucherNumber(request.Direction, request.VoucherDate)
            : request.VoucherNumber.Trim();
        voucher.PartyType = request.PartyType ?? CashPartyType.None;
        await ApplyPreparationAsync(
            voucher,
            preparation.Value,
            cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        if (preparation.Value.BusinessPartner is not null)
        {
            dbContext.BusinessPartnerMovements.Add(
                CreatePartnerMovement(voucher));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherResponse>> UpdateAsync(
        int id,
        CashVoucherUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashVoucherResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherResponse>.Failure(NotFound(id));
        }

        if (!voucher.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        var preparation = await PrepareAsync(
            request,
            voucher,
            cancellationToken);
        if (preparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(preparation.Error);
        }

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;

        var voucherNumber = voucher.VoucherNumber;
        request.Adapt(voucher);
        voucher.VoucherNumber = voucherNumber;
        voucher.PartyType = request.PartyType ?? CashPartyType.None;
        await ApplyPreparationAsync(
            voucher,
            preparation.Value,
            cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
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
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result.Failure(InvoiceGeneratedReadOnly());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            voucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
        if (balanceError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(balanceError);
        }

        try
        {
            var partnerMovements = await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == id)
                .ToListAsync(cancellationToken);
            dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
            dbContext.CashVouchers.Remove(voucher);
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

    private async Task<Result<VoucherPreparation>> PrepareAsync(
        CashVoucherRequest request,
        CashVoucher? currentVoucher,
        CancellationToken cancellationToken)
    {
        if (request.CashboxId.HasValue !=
            request.CashMovementTypeId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                PostingReferencesMustBeTogether());
        }

        if (!request.CashboxId.HasValue)
        {
            var draftBalanceError = await ValidateFinalBalancesAsync(
                currentVoucher,
                proposedCashboxId: null,
                proposedDirection: null,
                proposedAmount: null,
                cancellationToken);
            if (draftBalanceError is not null)
            {
                return Result<VoucherPreparation>.Failure(
                    draftBalanceError);
            }

            return Result<VoucherPreparation>.Success(
                new VoucherPreparation(
                    Cashbox: null,
                    BusinessPartner: null,
                    Driver: null,
                    ExchangeRate: null));
        }

        var cashboxId = request.CashboxId.Value;
        var cashMovementTypeId = request.CashMovementTypeId!.Value;
        var partyType = request.PartyType ?? CashPartyType.None;

        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == cashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<VoucherPreparation>.Failure(
                CashboxNotFound(cashboxId));
        }

        if (!cashbox.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashboxId != cashbox.Id))
        {
            return Result<VoucherPreparation>.Failure(CashboxInactive());
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            cashbox.Currency,
            request.VoucherDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<VoucherPreparation>.Failure(
                exchangeRateResult.Error);
        }

        var movementType = await dbContext.CashMovementTypes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == cashMovementTypeId,
                cancellationToken);
        if (movementType is null)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeNotFound(cashMovementTypeId));
        }

        if (!movementType.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashMovementTypeId != movementType.Id))
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeInactive());
        }

        if (movementType.Direction != request.Direction)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeDirectionMismatch());
        }

        BusinessPartner? partner = null;
        if (partyType == CashPartyType.Partner)
        {
            if (movementType.PartnerEffect == PartnerAccountEffect.None)
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeNotForPartner());
            }

            partner = await dbContext.BusinessPartners
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.BusinessPartnerId &&
                        entity.IsActive,
                    cancellationToken);
            if (partner is null)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerNotFound(request.BusinessPartnerId));
            }

            if (partner.Currency != cashbox.Currency)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerCurrencyMismatch());
            }
        }
        else if (movementType.PartnerEffect != PartnerAccountEffect.None)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeForPartnerOnly());
        }

        Driver? driver = null;
        if (partyType == CashPartyType.Driver)
        {
            driver = await dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.DriverId &&
                        entity.IsActive,
                    cancellationToken);
            if (driver is null)
            {
                return Result<VoucherPreparation>.Failure(
                    DriverNotFound(request.DriverId));
            }

            if (request.DriverTripId.HasValue)
            {
                var tripExists = await dbContext.DriverTrips
                    .AsNoTracking()
                    .AnyAsync(
                        trip =>
                            trip.CompanyId == companyId &&
                            trip.Id == request.DriverTripId.Value &&
                            trip.DriverId == driver.Id,
                        cancellationToken);
                if (!tripExists)
                {
                    return Result<VoucherPreparation>.Failure(
                        DriverTripNotFound(request.DriverTripId.Value));
                }
            }
        }

        var balanceError = await ValidateFinalBalancesAsync(
            currentVoucher,
            cashboxId,
            request.Direction,
            request.Amount,
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<VoucherPreparation>.Failure(balanceError);
        }

        return Result<VoucherPreparation>.Success(
            new VoucherPreparation(
                cashbox,
                partner,
                driver,
                exchangeRateResult.Value));
    }

    private async Task<Result<VoucherPreparation>> PrepareAsync(
        CashVoucherUpdateRequest request,
        CashVoucher currentVoucher,
        CancellationToken cancellationToken)
    {
        var createShape = new CashVoucherRequest(
            request.VoucherNumber,
            request.VoucherDate,
            request.Direction,
            request.CashboxId,
            request.CashMovementTypeId,
            request.PartyType,
            request.BusinessPartnerId,
            request.DriverId,
            request.DriverTripId,
            request.ExternalPartyName,
            request.Amount,
            request.ReferenceNumber,
            request.Description,
            request.Notes,
            request.ExchangeRate);

        return await PrepareAsync(
            createShape,
            currentVoucher,
            cancellationToken);
    }

    private async Task<Error?> ValidateFinalBalancesAsync(
        CashVoucher? currentVoucher,
        int? proposedCashboxId,
        CashDirection? proposedDirection,
        decimal? proposedAmount,
        CancellationToken cancellationToken)
    {
        var affectedCashboxIds = new HashSet<int>();
        if (currentVoucher?.CashboxId is int currentCashboxId)
        {
            affectedCashboxIds.Add(currentCashboxId);
        }

        if (proposedCashboxId.HasValue)
        {
            affectedCashboxIds.Add(proposedCashboxId.Value);
        }

        foreach (var cashboxId in affectedCashboxIds)
        {
            var excludedVoucherId = currentVoucher?.Id;
            var balance = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.Id == cashboxId)
                .Select(cashbox =>
                    cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            !excludedVoucherId.HasValue ||
                            voucher.Id != excludedVoucherId.Value)
                        .Sum(voucher =>
                            (decimal?)(voucher.Direction ==
                                CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount)) ?? 0m))
                .SingleAsync(cancellationToken);

            if (proposedCashboxId == cashboxId &&
                proposedDirection.HasValue &&
                proposedAmount.HasValue)
            {
                balance += proposedDirection == CashDirection.Receipt
                    ? proposedAmount.Value
                    : -proposedAmount.Value;
            }

            if (balance < 0m)
            {
                return InsufficientCashboxBalance(cashboxId);
            }
        }

        return null;
    }

    private BusinessPartnerMovement CreatePartnerMovement(
        CashVoucher voucher)
    {
        var debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        var credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;

        var movement = new BusinessPartnerMovement
        {
            CompanyId = companyId,
            BusinessPartnerId = voucher.BusinessPartnerId!.Value,
            CashVoucherId = voucher.Id,
            CashVoucher = voucher,
            MovementType = voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment,
            MovementDate = voucher.VoucherDate,
            Currency = voucher.Currency,
            Debit = debit,
            Credit = credit,
            Description = voucher.Description ??
                $"Cash voucher {voucher.VoucherNumber}"
        };
        movement.ApplyExchangeRate(voucher.ExchangeRate);
        return movement;
    }

    private async Task SynchronizePartnerMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(
                movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist)
        {
            if (existing is not null)
            {
                dbContext.BusinessPartnerMovements.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            dbContext.BusinessPartnerMovements.Add(
                CreatePartnerMovement(voucher));
            return;
        }

        existing.BusinessPartnerId = voucher.BusinessPartnerId!.Value;
        existing.MovementType =
            voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.Currency;
        existing.Debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        existing.Credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;
        existing.ApplyExchangeRate(voucher.ExchangeRate);
        existing.Description = voucher.Description ??
            $"Cash voucher {voucher.VoucherNumber}";
    }

    private IQueryable<CashVoucherResponse> ProjectResponseQuery(int id) =>
        dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.Id == id)
            .ProjectToType<CashVoucherResponse>();

    private async Task ApplyPreparationAsync(
        CashVoucher voucher,
        VoucherPreparation preparation,
        CancellationToken cancellationToken)
    {
        if (preparation.Cashbox is not null &&
            preparation.ExchangeRate is not null)
        {
            voucher.Currency = preparation.Cashbox.Currency;
            voucher.ApplyExchangeRate(
                preparation.ExchangeRate.ExchangeRateId,
                preparation.ExchangeRate.Rate);
            return;
        }

        voucher.CashboxId = null;
        voucher.CashMovementTypeId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.BusinessPartnerId = null;
        voucher.DriverId = null;
        voucher.DriverTripId = null;
        voucher.ExternalPartyName = null;

        voucher.Currency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
    }

    private static string GenerateVoucherNumber(
        CashDirection direction,
        DateOnly voucherDate)
    {
        var prefix = direction == CashDirection.Receipt ? "RCV" : "PAY";
        var suffix = Guid.NewGuid()
            .ToString("N")[..8]
            .ToUpperInvariant();
        return $"{prefix}-{voucherDate:yyyyMMdd}-{suffix}";
    }

    private sealed record VoucherPreparation(
        Cashbox? Cashbox,
        BusinessPartner? BusinessPartner,
        Driver? Driver,
        ResolvedExchangeRate? ExchangeRate);

}
