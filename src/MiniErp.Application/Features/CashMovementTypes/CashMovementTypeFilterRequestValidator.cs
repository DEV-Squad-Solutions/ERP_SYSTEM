using FluentValidation;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed class CashMovementTypeFilterRequestValidator
    : AbstractValidator<CashMovementTypeFilterRequest>
{
    public CashMovementTypeFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.Name)
            .MaximumLength(CashMovementTypeRequest.NameMaximumLength);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.PartnerEffect)
            .IsInEnum()
            .When(filter => filter.PartnerEffect.HasValue);
    }
}

public sealed class CashMovementTypeSelectRequestValidator
    : AbstractValidator<CashMovementTypeSelectRequest>
{
    public CashMovementTypeSelectRequestValidator()
    {
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
    }
}
