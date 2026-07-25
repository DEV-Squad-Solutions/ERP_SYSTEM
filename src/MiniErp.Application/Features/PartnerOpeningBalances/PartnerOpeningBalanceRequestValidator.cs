using FluentValidation;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceRequestValidator
    : AbstractValidator<PartnerOpeningBalanceRequest>
{
    public PartnerOpeningBalanceRequestValidator()
    {
        PartnerOpeningBalanceValidationRules.Add(this);
    }
}

public sealed class PartnerOpeningBalanceUpdateRequestValidator
    : AbstractValidator<PartnerOpeningBalanceUpdateRequest>
{
    public PartnerOpeningBalanceUpdateRequestValidator()
    {
        PartnerOpeningBalanceValidationRules.Add(this);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: > 0 })
            .WithMessage("يجب إرسال إصدار السجل الحالي للتعديل.");
    }
}

internal static class PartnerOpeningBalanceValidationRules
{
    public static void Add<T>(AbstractValidator<T> validator)
        where T : IPartnerOpeningBalanceRequest
    {
        validator.RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0);

        validator.RuleFor(request => request.DocumentNumber)
            .NotEmpty()
            .Must(number => !string.IsNullOrWhiteSpace(number))
            .WithMessage("رقم المستند مطلوب.")
            .MaximumLength(PartnerOpeningBalanceRequest.DocumentNumberMaximumLength);

        validator.RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        validator.RuleFor(request => request.Currency)
            .IsInEnum();

        validator.RuleFor(request => request.BalanceType)
            .IsInEnum();

        validator.RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true)
            .Must(PartnerOpeningBalanceAmountRules.IsValidAmount)
            .WithMessage("يجب أن يكون المبلغ موجباً وبحد أقصى منزلتين عشريتين.");

        validator.RuleFor(request => request.Notes)
            .MaximumLength(PartnerOpeningBalanceRequest.NotesMaximumLength);
    }
}
