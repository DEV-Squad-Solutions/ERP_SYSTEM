using FluentValidation;

namespace MiniErp.Application.Features.Containers;

public sealed class ContainerRequestValidator
    : AbstractValidator<ContainerRequest>
{
    public ContainerRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1_000);
    }
}
