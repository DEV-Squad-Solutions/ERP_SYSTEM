using FluentValidation;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryFilterRequestValidator : AbstractValidator<CountryFilterRequest>
{
    public CountryFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(200);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.EnglishName).MaximumLength(200);
    }
}
