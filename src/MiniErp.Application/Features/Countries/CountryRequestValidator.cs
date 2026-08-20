using FluentValidation;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryRequestValidator : AbstractValidator<CountryRequest>
{
    public CountryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request =>
                request.Name is not null &&
                request.Name.Trim().Length > 200);

        RuleFor(request => request.EnglishName)
            .NotEmpty();

        RuleFor(request => request.EnglishName)
            .MaximumLength(200)
            .When(request =>
                request.EnglishName is not null &&
                request.EnglishName.Trim().Length > 200);
    }
}
