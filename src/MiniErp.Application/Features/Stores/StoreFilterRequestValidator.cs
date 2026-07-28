using FluentValidation;

namespace MiniErp.Application.Features.Stores;

public sealed class StoreFilterRequestValidator : AbstractValidator<StoreFilterRequest>
{
    public StoreFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(500);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);
    }
}
