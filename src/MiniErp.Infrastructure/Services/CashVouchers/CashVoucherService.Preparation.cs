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
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed partial class CashVoucherService
{
    private async Task<string> GenerateVoucherNumberAsync(
        CashDirection direction,
        CancellationToken cancellationToken)
    {
        var prefix = direction == CashDirection.Receipt ? "RCV" : "PAY";
        return await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix,
            companyId,
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
    }

    private static CashVoucherUpdateRequest ToUpdateRequest(
        CashVoucherBulkVoucherRequest request,
        byte[]? rowVersion) =>
        new(
            VoucherDate: request.VoucherDate,
            Direction: request.Direction,
            CashboxId: request.CashboxId,
            CashMovementTypeId: request.CashMovementTypeId,
            EmployeeId: request.EmployeeId,
            BusinessPartnerId: request.BusinessPartnerId,
            DriverId: request.DriverId,
            DriverTripId: request.DriverTripId,
            ExternalPartyName: request.ExternalPartyName,
            Amount: request.Amount,
            ReferenceNumber: request.ReferenceNumber,
            Description: request.Description,
            Notes: request.Notes,
            RowVersion: rowVersion,
            ExchangeRate: request.ExchangeRate,
            AccountId: request.AccountId,
            EmployeeMovementType: request.EmployeeMovementType,
            PartnerExchangeRate: request.PartnerExchangeRate);

    private async Task<CashVoucherUpdateRequest> NormalizeEmployeeMovementTypeAsync(
        CashVoucherUpdateRequest request,
        CashVoucher? currentVoucher,
        CancellationToken cancellationToken)
    {
        if (!request.EmployeeId.HasValue ||
            request.EmployeeMovementType.HasValue)
        {
            return request;
        }

        EmployeeMovementType? existingType = null;
        if (currentVoucher is not null)
        {
            existingType = await dbContext.EmployeeMovements
                .AsNoTracking()
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == currentVoucher.Id)
                .Select(movement => (EmployeeMovementType?)movement.Type)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var isReceipt = request.Direction == CashDirection.Receipt;
        var effectiveType = existingType.HasValue &&
            EmployeeAccountRules.IsCreditMovement(existingType.Value) ==
            isReceipt
                ? existingType.Value
                : GetDefaultEmployeeMovementType(request.Direction);

        return request with { EmployeeMovementType = effectiveType };
    }

    private async Task<Result> EnsureVoucherExecuteAsync(CashVoucher voucher, CancellationToken cancellationToken)
    {
        if (voucher.CashboxId is int cashboxId)
            return await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, cashboxId, cancellationToken);
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        return access.IsFailure ? Result.Failure(access.Errors)
            : access.Value.HasAllCashboxes ? Result.Success()
            : Result.Failure(CashboxPermissionGuard.Forbidden());
    }

    private static EmployeeMovementType GetDefaultEmployeeMovementType(
        CashDirection direction) =>
        direction == CashDirection.Receipt
            ? EmployeeMovementType.Credit
            : EmployeeMovementType.Debit;

    private async Task<Result<VoucherPreparation>> PrepareAsync(
        CashVoucherUpdateRequest request,
        CashVoucher? currentVoucher,
        bool enforceManualPostingTarget,
        CancellationToken cancellationToken)
    {
        if (request.PartnerExchangeRate.HasValue && !request.BusinessPartnerId.HasValue)
            return Result<VoucherPreparation>.Failure(Error.Validation(
                "CashVouchers.PartnerExchangeRateNotAllowed",
                "سعر صرف الطرف يستخدم مع سند الشريك فقط.",
                nameof(request.PartnerExchangeRate)));
        if (!request.CashboxId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                PostingReferencesMustBeTogether());
        }

        var cashboxId = request.CashboxId.Value;
        var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, cashboxId, cancellationToken);
        if (access.IsFailure) return Result<VoucherPreparation>.Failure(access.Errors);
        if (enforceManualPostingTarget &&
            !HasExactlyOnePostingTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (!enforceManualPostingTarget && !HasAtMostOneTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (request.DriverTripId.HasValue && !request.DriverId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                DriverTripRequiresDriver());
        }

        var partyType = DerivePartyType(request);

        if (partyType == CashPartyType.Employee)
        {
            if (!request.EmployeeMovementType.HasValue)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeRequired());
            }

            if (!Enum.IsDefined(request.EmployeeMovementType.Value))
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeInvalid());
            }

            var isReceipt = request.Direction == CashDirection.Receipt;
            if (EmployeeAccountRules.IsCreditMovement(
                    request.EmployeeMovementType.Value) != isReceipt)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeDirectionMismatch());
            }
        }
        else if (request.EmployeeMovementType.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                EmployeeMovementTypeNotAllowed());
        }

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

        // A draft may already contain the manual rate agreed at cash handover.
        // Preserve that snapshot only for the same currency and voucher date;
        // completed vouchers and changed currency/date resolve afresh. The
        // legacy draft placeholder is rate 1 with no base amount or rate id.
        var requestedRate = request.ExchangeRate;
        if (!requestedRate.HasValue &&
            currentVoucher is { IsPosted: false } &&
            currentVoucher.Currency == cashbox.Currency &&
            currentVoucher.VoucherDate == request.VoucherDate &&
            (currentVoucher.ExchangeRate != 1m ||
             currentVoucher.BaseAmount != 0m ||
             currentVoucher.ExchangeRateId.HasValue))
        {
            requestedRate = currentVoucher.ExchangeRate;
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            currency: cashbox.Currency,
            date: request.VoucherDate,
            requestedRate: requestedRate,
            cancellationToken: cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<VoucherPreparation>.Failure(
                exchangeRateResult.Error);
        }

        CashMovementType? movementType = null;
        if (request.CashMovementTypeId is int cashMovementTypeId)
        {
            movementType = await dbContext.CashMovementTypes
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
                (enforceManualPostingTarget ||
                 currentVoucher is null ||
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

        }

        Account? account = null;
        if (request.AccountId is int accountId)
        {
            account = await dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == accountId,
                    cancellationToken);
            if (account is null)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountNotFound(accountId));
            }

            if (!account.IsActive || !account.IsPosting)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountInactiveOrNotPosting());
            }

            var accountMatchesDirection =
                (request.Direction == CashDirection.Payment &&
                 account.AccountType == AccountType.Expense) ||
                (request.Direction == CashDirection.Receipt &&
                 account.AccountType == AccountType.Revenue);
            if (!accountMatchesDirection)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountDirectionMismatch());
            }
        }

        BusinessPartner? partner = null;
        ResolvedExchangeRate? partnerExchangeRate = null;
        if (partyType == CashPartyType.Partner)
        {
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

            if (partner.Currency == cashbox.Currency)
            {
                if (request.PartnerExchangeRate.HasValue &&
                    request.PartnerExchangeRate != exchangeRateResult.Value.Rate)
                    return Result<VoucherPreparation>.Failure(Error.Validation(
                        "CashVouchers.PartnerRateMismatch",
                        "سعر صرف الطرف يجب أن يساوي سعر صرف الخزينة عند تطابق العملة.",
                        nameof(request.PartnerExchangeRate)));
                partnerExchangeRate = exchangeRateResult.Value;
            }
            else
            {
                var requestedPartnerRate = request.PartnerExchangeRate;
                if (!requestedPartnerRate.HasValue &&
                    currentVoucher?.BusinessPartnerId == partner.Id &&
                    currentVoucher.PartnerExchangeRateId is null &&
                    currentVoucher.PartnerCurrency == partner.Currency &&
                    currentVoucher.VoucherDate == request.VoucherDate)
                    requestedPartnerRate = currentVoucher.PartnerExchangeRate;
                var partnerRateResult = await exchangeRateResolver.ResolveAsync(
                    currency: partner.Currency,
                    date: request.VoucherDate,
                    requestedRate: requestedPartnerRate,
                    cancellationToken: cancellationToken);
                if (partnerRateResult.IsFailure)
                    return Result<VoucherPreparation>.Failure(partnerRateResult.Errors
                        .Select(error => error with { FieldName = nameof(request.PartnerExchangeRate) })
                        .ToArray());
                partnerExchangeRate = partnerRateResult.Value;
                if (ExchangeRateRules.ConvertFromBase(
                        ExchangeRateRules.ConvertToBase(request.Amount, exchangeRateResult.Value.Rate),
                        partnerExchangeRate.Rate) <= 0m)
                    return Result<VoucherPreparation>.Failure(Error.Validation(
                        "CashVouchers.PartnerAmountTooSmall",
                        "مقابل السداد بعملة الطرف أقل من أصغر قيمة قابلة للتسجيل.",
                        nameof(request.Amount)));
            }
        }
        else if (movementType is not null &&
                 movementType.PartnerEffect != PartnerAccountEffect.None)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeForPartnerOnly());
        }

        if (partyType == CashPartyType.Employee)
        {
            var employeeExists = await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.EmployeeId &&
                        entity.IsActive,
                    cancellationToken);
            if (!employeeExists)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeNotFound(request.EmployeeId));
            }
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
                var tripFiscalYearId = currentVoucher?.FiscalYearId;
                if (!tripFiscalYearId.HasValue)
                {
                    tripFiscalYearId = await dbContext.FiscalYears.AsNoTracking()
                        .Where(year => year.CompanyId == companyId &&
                            year.StartDate <= request.VoucherDate && year.EndDate >= request.VoucherDate)
                        .Select(year => (int?)year.Id)
                        .SingleOrDefaultAsync(cancellationToken);
                }
                var tripExists = await dbContext.DriverTrips
                    .AsNoTracking()
                    .AnyAsync(
                        trip =>
                            trip.CompanyId == companyId &&
                            trip.FiscalYearId == tripFiscalYearId &&
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

        return Result<VoucherPreparation>.Success(
            new VoucherPreparation(
                Cashbox: cashbox,
                PartyType: partyType,
                BusinessPartner: partner,
                Driver: driver,
                ExchangeRate: exchangeRateResult.Value,
                PartnerExchangeRate: partnerExchangeRate,
                Classification: account?.AccountType switch
                    {
                        AccountType.Expense =>
                            CashMovementClassification.Expense,
                        AccountType.Revenue =>
                            CashMovementClassification.Revenue,
                        _ => movementType?.Classification
                    }));
    }
}
