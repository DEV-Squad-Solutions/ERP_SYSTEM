using FluentValidation;

namespace MiniErp.Application.Features.ItemUnits;

public sealed class ItemUnitRequestValidator : AbstractValidator<ItemUnitRequest>
{
    public ItemUnitRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Name)
            .MaximumLength(100)
            .When(request =>
                request.Name is not null &&
                request.Name.Trim().Length > 100);
    }
}
