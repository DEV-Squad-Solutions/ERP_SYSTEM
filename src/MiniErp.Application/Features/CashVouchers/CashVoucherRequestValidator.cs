using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherRequestValidator
    : AbstractValidator<CashVoucherRequest>
{
    public CashVoucherRequestValidator()
    {
        RuleFor(request => request.VoucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.CashboxId)
            .GreaterThan(0);

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.Description)
            .MaximumLength(CashVoucherRequest.DescriptionMaximumLength);
    }
}

public sealed class CashVoucherUpdateRequestValidator
    : AbstractValidator<CashVoucherUpdateRequest>
{
    public CashVoucherUpdateRequestValidator()
    {
        RuleFor(request => request.VoucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.CashboxId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("اختر صندوق النقدية لاستكمال السند.");

        RuleFor(request => request.CashMovementTypeId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("اختر نوع الحركة لاستكمال السند.");

        RuleFor(request => request.PartyType)
            .NotNull()
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("اختر نوع الطرف لاستكمال السند.");

        RuleFor(request => request.BusinessPartnerId)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.PartyType == CashPartyType.Partner);
        RuleFor(request => request.BusinessPartnerId)
            .Null()
            .When(request => request.PartyType != CashPartyType.Partner);

        RuleFor(request => request.DriverId)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.PartyType == CashPartyType.Driver);
        RuleFor(request => request.DriverId)
            .Null()
            .When(request => request.PartyType != CashPartyType.Driver);

        RuleFor(request => request.DriverTripId)
            .GreaterThan(0)
            .When(request => request.DriverTripId.HasValue);
        RuleFor(request => request.DriverTripId)
            .Null()
            .When(request => request.PartyType != CashPartyType.Driver);

        RuleFor(request => request.EmployeeId)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.PartyType == CashPartyType.Employee);
        RuleFor(request => request.EmployeeId)
            .Null()
            .When(request => request.PartyType != CashPartyType.Employee);

        RuleFor(request => request.ExternalPartyName)
            .NotEmpty()
            .MaximumLength(
                CashVoucherRequest.ExternalPartyNameMaximumLength)
            .When(request => request.PartyType == CashPartyType.Other);
        RuleFor(request => request.ExternalPartyName)
            .Null()
            .When(request => request.PartyType != CashPartyType.Other);

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.ReferenceNumber)
            .MaximumLength(
                CashVoucherRequest.ReferenceNumberMaximumLength);
        RuleFor(request => request.Description)
            .MaximumLength(
                CashVoucherRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes)
            .MaximumLength(CashVoucherRequest.NotesMaximumLength);

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
