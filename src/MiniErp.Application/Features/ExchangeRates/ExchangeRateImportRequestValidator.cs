using FluentValidation;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed class ExchangeRateImportRequestValidator
    : AbstractValidator<ExchangeRateImportRequest>
{
    public const int MaximumFutureDays = 7;

    public ExchangeRateImportRequestValidator(TimeProvider timeProvider)
    {
        var maximumDate = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime.AddDays(MaximumFutureDays));

        RuleFor(request => request.RateDate)
            .NotEmpty()
            .LessThanOrEqualTo(maximumDate)
            .WithMessage(
                $"Rate date cannot be more than {MaximumFutureDays} days in the future.");

        RuleFor(request => request.Currencies)
            .Must(currencies =>
                currencies is null ||
                currencies.All(Enum.IsDefined))
            .WithMessage("One or more requested currencies are invalid.");

        RuleFor(request => request.Currencies)
            .Must(currencies =>
                currencies is null ||
                currencies.Distinct().Count() == currencies.Count)
            .WithMessage("Requested currencies must not contain duplicates.");
    }
}
