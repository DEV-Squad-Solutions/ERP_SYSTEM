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

        RuleFor(request => request.EmployeeId)
            .GreaterThan(0)
            .When(request => request.EmployeeId.HasValue);

        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0)
            .When(request => request.BusinessPartnerId.HasValue);

        RuleFor(request => request.DriverId)
            .GreaterThan(0)
            .When(request => request.DriverId.HasValue);

        RuleFor(request => request.DriverTripId)
            .GreaterThan(0)
            .When(request => request.DriverTripId.HasValue);
        RuleFor(request => request)
            .Must(request =>
                !request.DriverTripId.HasValue || request.DriverId.HasValue)
            .WithMessage("اختر السائق قبل اختيار الرحلة.")
            .WithName(nameof(CashVoucherUpdateRequest.DriverTripId));

        RuleFor(request => request.ExternalPartyName)
            .MaximumLength(
                CashVoucherRequest.ExternalPartyNameMaximumLength)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("اسم الطرف الخارجي لا يمكن أن يكون فارغاً.");

        RuleFor(request => request)
            .Must(HasAtMostOneParty)
            .WithMessage("اختر طرفاً واحداً فقط للسند.")
            .OverridePropertyName(
                nameof(CashVoucherUpdateRequest.EmployeeId));

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

    private static bool HasAtMostOneParty(CashVoucherUpdateRequest request)
    {
        var selectedPartyCount =
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyCount <= 1;
    }
}
