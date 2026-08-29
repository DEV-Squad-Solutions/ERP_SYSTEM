using FluentValidation;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed class EmployeeOpeningBalanceRequestValidator
    : AbstractValidator<EmployeeOpeningBalanceRequest>
{
    public EmployeeOpeningBalanceRequestValidator()
    {
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");

        RuleFor(request => request.EmployeeId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Currency)
            .Equal(CurrencyCode.EGP)
            .WithMessage("عملة الرصيد الافتتاحي للموظف يجب أن تكون دائماً بالجنيه المصري (EGP).");

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
            .MaximumLength(EmployeeOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                EmployeeOpeningBalanceRequest.NotesMaximumLength);
    }
}

public sealed class EmployeeOpeningBalanceUpdateRequestValidator
    : AbstractValidator<EmployeeOpeningBalanceUpdateRequest>
{
    public EmployeeOpeningBalanceUpdateRequestValidator()
    {
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");

        RuleFor(request => request.EmployeeId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Currency)
            .Equal(CurrencyCode.EGP)
            .WithMessage("عملة الرصيد الافتتاحي للموظف يجب أن تكون دائماً بالجنيه المصري (EGP).");

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
            .MaximumLength(EmployeeOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                EmployeeOpeningBalanceRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: > 0 })
            .WithMessage("يجب إرسال إصدار السجل الحالي للتعديل.");
    }
}
