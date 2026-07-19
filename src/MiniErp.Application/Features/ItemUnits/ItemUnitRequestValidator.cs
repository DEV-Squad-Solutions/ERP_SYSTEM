using FluentValidation;

namespace MiniErp.Application.Features.ItemUnits;

public sealed class ItemUnitRequestValidator : AbstractValidator<ItemUnitRequest>
{
    public ItemUnitRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
