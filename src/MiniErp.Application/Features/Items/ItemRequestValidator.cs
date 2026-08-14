using FluentValidation;

namespace MiniErp.Application.Features.Items;

public sealed class ItemRequestValidator : AbstractValidator<ItemRequest>
{
    public ItemRequestValidator()
    {
        RuleFor(request => request.ItemUnitId)
            .GreaterThan(0);

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
