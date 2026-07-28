using FluentValidation;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryRequestValidator : AbstractValidator<CountryRequest>
{
    public CountryRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty();

        RuleFor(request => request.Code)
            .MaximumLength(50)
            .When(request =>
                request.Code is not null &&
                request.Code.Trim().Length > 50);

        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request =>
                request.Name is not null &&
                request.Name.Trim().Length > 200);

        RuleFor(request => request.ArabicName)
            .NotEmpty();

        RuleFor(request => request.ArabicName)
            .MaximumLength(200)
            .When(request =>
                request.ArabicName is not null &&
                request.ArabicName.Trim().Length > 200);
    }
}
