using FluentValidation;

namespace MiniErp.Application.Features.Items;

public sealed class ItemRequestValidator : AbstractValidator<ItemRequest>
{
    public ItemRequestValidator()
    {
        RuleFor(request => request.ItemUnitId)
            .GreaterThan(0);

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
