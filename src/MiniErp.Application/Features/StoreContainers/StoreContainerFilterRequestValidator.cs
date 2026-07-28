using FluentValidation;

namespace MiniErp.Application.Features.StoreContainers;

public sealed class StoreContainerFilterRequestValidator
    : AbstractValidator<StoreContainerFilterRequest>
{
    public StoreContainerFilterRequestValidator()
    {
        RuleFor(filter => filter.StoreId)
            .GreaterThan(0)
            .When(filter => filter.StoreId.HasValue);
        RuleFor(filter => filter.ContainerId)
            .GreaterThan(0)
            .When(filter => filter.ContainerId.HasValue);
        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);
    }
}
