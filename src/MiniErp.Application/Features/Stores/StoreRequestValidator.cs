using FluentValidation;

namespace MiniErp.Application.Features.Stores;

public sealed class StoreRequestValidator : AbstractValidator<StoreRequest>
{
    public StoreRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Address)
            .MaximumLength(500);
    }
}
