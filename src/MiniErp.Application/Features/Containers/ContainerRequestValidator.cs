using FluentValidation;

namespace MiniErp.Application.Features.Containers;

public sealed class ContainerRequestValidator
    : AbstractValidator<ContainerRequest>
{
    public ContainerRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Must(code => !string.IsNullOrWhiteSpace(code))
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1_000);
    }
}
