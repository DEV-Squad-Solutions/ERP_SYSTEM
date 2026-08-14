using FluentValidation;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceRequestValidator
    : AbstractValidator<PartnerOpeningBalanceRequest>
{
    public PartnerOpeningBalanceRequestValidator()
    {
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");

        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.BalanceType)
            .IsInEnum();

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true)
            .Must(PartnerOpeningBalanceAmountRules.IsValidAmount)
            .WithMessage("يجب أن يكون المبلغ موجباً وبحد أقصى منزلتين عشريتين.");

        RuleFor(request => request.Notes)
            .MaximumLength(PartnerOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                PartnerOpeningBalanceRequest.NotesMaximumLength);
    }
}

public sealed class PartnerOpeningBalanceUpdateRequestValidator
    : AbstractValidator<PartnerOpeningBalanceUpdateRequest>
{
    public PartnerOpeningBalanceUpdateRequestValidator()
    {
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");

        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.BalanceType)
            .IsInEnum();

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true)
            .Must(PartnerOpeningBalanceAmountRules.IsValidAmount)
            .WithMessage("يجب أن يكون المبلغ موجباً وبحد أقصى منزلتين عشريتين.");

        RuleFor(request => request.Notes)
            .MaximumLength(PartnerOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                PartnerOpeningBalanceRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: > 0 })
            .WithMessage("يجب إرسال إصدار السجل الحالي للتعديل.");
    }
}
