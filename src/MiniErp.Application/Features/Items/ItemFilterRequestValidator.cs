using FluentValidation;

namespace MiniErp.Application.Features.Items;

public sealed class ItemFilterRequestValidator : AbstractValidator<ItemFilterRequest>
{
    public ItemFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(1_000);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.ItemUnitId)
            .GreaterThan(0)
            .When(filter => filter.ItemUnitId.HasValue);
    }
}
