using FluentValidation;

namespace MiniErp.Application.Features.Containers;

public sealed class ContainerFilterRequestValidator : AbstractValidator<ContainerFilterRequest>
{
    public ContainerFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(1_000);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
    }
}
