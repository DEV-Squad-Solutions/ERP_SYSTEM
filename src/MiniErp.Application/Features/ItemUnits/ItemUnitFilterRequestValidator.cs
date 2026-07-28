using FluentValidation;

namespace MiniErp.Application.Features.ItemUnits;

public sealed class ItemUnitFilterRequestValidator : AbstractValidator<ItemUnitFilterRequest>
{
    public ItemUnitFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(100);
        RuleFor(filter => filter.Name).MaximumLength(100);
    }
}
