using FluentValidation;

namespace MiniErp.Application.Features.Containers;

public sealed class ContainerRequestValidator
    : AbstractValidator<ContainerRequest>
{
    public ContainerRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request =>
                request.Name is not null &&
                request.Name.Trim().Length > 200);

        RuleFor(request => request.Description)
            .MaximumLength(1_000)
            .When(request =>
                request.Description is not null &&
                request.Description.Trim().Length > 1_000);
    }
}
