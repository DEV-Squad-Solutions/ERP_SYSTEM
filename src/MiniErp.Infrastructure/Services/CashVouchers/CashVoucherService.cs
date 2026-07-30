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
                voucher.Cashbox.Code.Contains(search) ||
                voucher.Cashbox.Name.Contains(search) ||
                voucher.CashMovementType.Name.Contains(search) ||
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
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
        voucher.Currency = preparation.Value.Cashbox.Currency;
        voucher.ApplyExchangeRate(
            preparation.Value.ExchangeRate.ExchangeRateId,
            preparation.Value.ExchangeRate.Rate);
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

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

        request.Adapt(voucher);
        voucher.Currency = preparation.Value.Cashbox.Currency;
        voucher.ApplyExchangeRate(
            preparation.Value.ExchangeRate.ExchangeRateId,
            preparation.Value.ExchangeRate.Rate);
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

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == request.CashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<VoucherPreparation>.Failure(
                CashboxNotFound(request.CashboxId));
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
                    entity.Id == request.CashMovementTypeId,
                cancellationToken);
        if (movementType is null)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeNotFound(request.CashMovementTypeId));
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
        if (request.PartyType == CashPartyType.Partner)
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
        if (request.PartyType == CashPartyType.Driver)
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
            request.CashboxId,
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
        if (currentVoucher is not null)
        {
            affectedCashboxIds.Add(currentVoucher.CashboxId);
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

    private sealed record VoucherPreparation(
        Cashbox Cashbox,
        BusinessPartner? BusinessPartner,
        Driver? Driver,
        ResolvedExchangeRate ExchangeRate);

    private static Error InvalidId() =>
        Error.Validation(
            "CashVouchers.InvalidId",
            "يجب أن يكون رقم سند النقدية أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "CashVouchers.NotFound",
            $"لم يتم العثور على سند النقدية رقم {id}.");

    private static Error RowVersionRequired() =>
        Error.Validation(
            "CashVouchers.RowVersionRequired",
            "يجب إرسال إصدار سند النقدية الحالي للتعديل.",
            nameof(CashVoucherUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "CashVouchers.Concurrency",
            "تم تعديل سند النقدية بواسطة مستخدم آخر. أعد تحميل السند ثم حاول مرة أخرى.");

    private static Error InvoiceGeneratedReadOnly() =>
        Error.Conflict(
            "CashVouchers.InvoiceGeneratedReadOnly",
            "لا يمكن تعديل أو حذف سند السداد المنشأ تلقائياً من الفاتورة؛ عدّل الفاتورة نفسها.");

    private static Error CashboxNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.CashboxNotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.",
            nameof(CashVoucherRequest.CashboxId));

    private static Error CashboxInactive() =>
        Error.Conflict(
            "CashVouchers.CashboxInactive",
            "لا يمكن استخدام صندوق نقدية غير نشط في سند جديد.",
            nameof(CashVoucherRequest.CashboxId));

    private static Error MovementTypeNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.MovementTypeNotFound",
            $"لم يتم العثور على نوع الحركة النقدية رقم {id}.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    private static Error MovementTypeInactive() =>
        Error.Conflict(
            "CashVouchers.MovementTypeInactive",
            "لا يمكن استخدام نوع حركة نقدية غير نشط في سند جديد.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    private static Error MovementTypeDirectionMismatch() =>
        Error.Conflict(
            "CashVouchers.MovementTypeDirectionMismatch",
            "اتجاه نوع الحركة النقدية لا يطابق اتجاه السند.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    private static Error MovementTypeNotForPartner() =>
        Error.Conflict(
            "CashVouchers.MovementTypeNotForPartner",
            "نوع الحركة النقدية المختار غير مخصص لحسابات العملاء أو الموردين.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    private static Error MovementTypeForPartnerOnly() =>
        Error.Conflict(
            "CashVouchers.MovementTypeForPartnerOnly",
            "نوع الحركة النقدية المختار مخصص للعملاء أو الموردين فقط.",
            nameof(CashVoucherRequest.PartyType));

    private static Error PartnerNotFound(int? id) =>
        Error.NotFound(
            "CashVouchers.PartnerNotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.",
            nameof(CashVoucherRequest.BusinessPartnerId));

    private static Error PartnerCurrencyMismatch() =>
        Error.Conflict(
            "CashVouchers.PartnerCurrencyMismatch",
            "عملة صندوق النقدية لا تطابق عملة العميل أو المورد.",
            nameof(CashVoucherRequest.BusinessPartnerId));

    private static Error DriverNotFound(int? id) =>
        Error.NotFound(
            "CashVouchers.DriverNotFound",
            $"لم يتم العثور على السائق رقم {id}.",
            nameof(CashVoucherRequest.DriverId));

    private static Error DriverTripNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.DriverTripNotFound",
            $"لم يتم العثور على رحلة رقم {id} تخص السائق المحدد.",
            nameof(CashVoucherRequest.DriverTripId));

    private static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "CashVouchers.InsufficientCashboxBalance",
            $"الرصيد المتاح في صندوق النقدية رقم {cashboxId} لا يسمح بهذه العملية.");
}
