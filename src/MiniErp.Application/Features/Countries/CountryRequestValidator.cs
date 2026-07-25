using FluentValidation;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryRequestValidator : AbstractValidator<CountryRequest>
{
    public CountryRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Must(code => !string.IsNullOrWhiteSpace(code))
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(200);

        RuleFor(request => request.ArabicName)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(200);
    }
}
