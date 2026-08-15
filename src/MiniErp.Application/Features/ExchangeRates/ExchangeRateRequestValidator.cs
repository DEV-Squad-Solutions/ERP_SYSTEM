using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed class ExchangeRateRequestValidator
    : AbstractValidator<ExchangeRateRequest>
{
    public ExchangeRateRequestValidator()
    {
        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.RateDate)
            .NotEmpty();

        RuleFor(request => request.Rate)
            .Must(ExchangeRateRules.IsValidRate)
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر وألا يتجاوز 12 منزلة عشرية.");

        RuleFor(request => request.Source)
            .Equal(ExchangeRateSource.Manual)
            .WithMessage("Normal exchange-rate creation only accepts Manual source.");

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}

public sealed class ExchangeRateUpdateRequestValidator
    : AbstractValidator<ExchangeRateUpdateRequest>
{
    public ExchangeRateUpdateRequestValidator()
    {
        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.RateDate)
            .NotEmpty();

        RuleFor(request => request.Rate)
            .Must(ExchangeRateRules.IsValidRate)
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر وألا يتجاوز 12 منزلة عشرية.");

        RuleFor(request => request.Source)
            .Equal(ExchangeRateSource.Manual)
            .WithMessage("Normal exchange-rate updates only accept Manual source.");

        RuleFor(request => request.Notes)
            .MaximumLength(500);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار سعر الصرف الحالي.");
    }
}

public sealed class ExchangeRateFilterRequestValidator
    : AbstractValidator<ExchangeRateFilterRequest>
{
    public ExchangeRateFilterRequestValidator()
    {
        RuleFor(request => request.Currency)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value));

        RuleFor(request => request.Source)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value));

        RuleFor(request => request.DateTo)
            .GreaterThanOrEqualTo(request => request.DateFrom)
            .When(request =>
                request.DateFrom.HasValue &&
                request.DateTo.HasValue);

        RuleFor(request => request.Search)
            .Must(value => value is null || value.Trim().Length <= 500)
            .WithMessage("Search must not exceed 500 characters.");
    }
}
