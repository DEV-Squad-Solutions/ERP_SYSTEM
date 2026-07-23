using FluentValidation;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryRequestValidator : AbstractValidator<CountryRequest>
{
    public CountryRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.ArabicName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
