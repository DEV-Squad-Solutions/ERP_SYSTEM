using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherRequestValidator
    : AbstractValidator<CashVoucherRequest>
{
    public CashVoucherRequestValidator()
    {
        CashVoucherValidationRules.AddRules(this);

        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف سند النقدية أكبر من صفر.");
    }
}

public sealed class CashVoucherUpdateRequestValidator
    : AbstractValidator<CashVoucherUpdateRequest>
{
    public CashVoucherUpdateRequestValidator()
    {
        CashVoucherValidationRules.AddRules(this);

        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف سند النقدية أكبر من صفر.");

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار سند النقدية الحالي للتعديل.");
    }
}

internal static class CashVoucherValidationRules
{
    public static void AddRules(
        AbstractValidator<CashVoucherRequest> validator)
    {
        AddRules<CashVoucherRequest>(
            validator,
            request => request.VoucherNumber,
            request => request.VoucherDate,
            request => request.Direction,
            request => request.CashboxId,
            request => request.CashMovementTypeId,
            request => request.PartyType,
            request => request.BusinessPartnerId,
            request => request.DriverId,
            request => request.DriverTripId,
            request => request.ExternalPartyName,
            request => request.Amount,
            request => request.ReferenceNumber,
            request => request.Description,
            request => request.Notes);
    }

    public static void AddRules(
        AbstractValidator<CashVoucherUpdateRequest> validator)
    {
        AddRules<CashVoucherUpdateRequest>(
            validator,
            request => request.VoucherNumber,
            request => request.VoucherDate,
            request => request.Direction,
            request => request.CashboxId,
            request => request.CashMovementTypeId,
            request => request.PartyType,
            request => request.BusinessPartnerId,
            request => request.DriverId,
            request => request.DriverTripId,
            request => request.ExternalPartyName,
            request => request.Amount,
            request => request.ReferenceNumber,
            request => request.Description,
            request => request.Notes);
    }

    private static void AddRules<TRequest>(
        AbstractValidator<TRequest> validator,
        System.Linq.Expressions.Expression<Func<TRequest, string?>>
            voucherNumber,
        System.Linq.Expressions.Expression<Func<TRequest, DateOnly>>
            voucherDate,
        System.Linq.Expressions.Expression<Func<TRequest, CashDirection>>
            direction,
        System.Linq.Expressions.Expression<Func<TRequest, int?>>
            cashboxId,
        System.Linq.Expressions.Expression<Func<TRequest, int?>>
            cashMovementTypeId,
        System.Linq.Expressions.Expression<Func<TRequest, CashPartyType?>>
            partyType,
        System.Linq.Expressions.Expression<Func<TRequest, int?>>
            businessPartnerId,
        System.Linq.Expressions.Expression<Func<TRequest, int?>>
            driverId,
        System.Linq.Expressions.Expression<Func<TRequest, int?>>
            driverTripId,
        System.Linq.Expressions.Expression<Func<TRequest, string?>>
            externalPartyName,
        System.Linq.Expressions.Expression<Func<TRequest, decimal>>
            amount,
        System.Linq.Expressions.Expression<Func<TRequest, string?>>
            referenceNumber,
        System.Linq.Expressions.Expression<Func<TRequest, string?>>
            description,
        System.Linq.Expressions.Expression<Func<TRequest, string?>>
            notes)
    {
        var cashboxIdAccessor = cashboxId.Compile();
        var cashMovementTypeIdAccessor = cashMovementTypeId.Compile();
        var partyTypeAccessor = partyType.Compile();
        var driverTripIdAccessor = driverTripId.Compile();

        validator.RuleFor(voucherNumber)
            .MaximumLength(CashVoucherRequest.VoucherNumberMaximumLength);

        validator.RuleFor(voucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        validator.RuleFor(direction).IsInEnum();
        validator.RuleFor(cashboxId)
            .GreaterThan(0)
            .When(request => cashboxIdAccessor(request).HasValue);
        validator.RuleFor(cashMovementTypeId)
            .GreaterThan(0)
            .When(request => cashMovementTypeIdAccessor(request).HasValue);
        validator.RuleFor(partyType)
            .Must(value =>
                !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage("نوع الطرف غير صالح.");

        validator.RuleFor(request => request)
            .Must(request =>
                cashboxIdAccessor(request).HasValue ==
                cashMovementTypeIdAccessor(request).HasValue)
            .WithMessage(
                "اختر الصندوق ونوع الحركة معًا عند استكمال السند.");

        validator.RuleFor(businessPartnerId)
            .NotNull()
            .GreaterThan(0)
            .When(request =>
                partyTypeAccessor(request) == CashPartyType.Partner);
        validator.RuleFor(businessPartnerId)
            .Null()
            .When(request =>
                partyTypeAccessor(request) != CashPartyType.Partner);

        validator.RuleFor(driverId)
            .NotNull()
            .GreaterThan(0)
            .When(request =>
                partyTypeAccessor(request) == CashPartyType.Driver);
        validator.RuleFor(driverId)
            .Null()
            .When(request =>
                partyTypeAccessor(request) != CashPartyType.Driver);

        validator.RuleFor(driverTripId)
            .GreaterThan(0)
            .When(request => driverTripIdAccessor(request).HasValue);
        validator.RuleFor(driverTripId)
            .Null()
            .When(request =>
                partyTypeAccessor(request) != CashPartyType.Driver);

        validator.RuleFor(externalPartyName)
            .NotEmpty()
            .MaximumLength(
                CashVoucherRequest.ExternalPartyNameMaximumLength)
            .When(request =>
                partyTypeAccessor(request) == CashPartyType.Other);
        validator.RuleFor(externalPartyName)
            .Null()
            .When(request =>
                partyTypeAccessor(request) != CashPartyType.Other);

        validator.RuleFor(amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        validator.RuleFor(referenceNumber)
            .MaximumLength(
                CashVoucherRequest.ReferenceNumberMaximumLength);
        validator.RuleFor(description)
            .MaximumLength(
                CashVoucherRequest.DescriptionMaximumLength);
        validator.RuleFor(notes)
            .MaximumLength(CashVoucherRequest.NotesMaximumLength);
    }
}
