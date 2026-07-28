using FluentValidation;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed class BusinessPartnerFilterRequestValidator
    : AbstractValidator<BusinessPartnerFilterRequest>
{
    public BusinessPartnerFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.TaxNumber).MaximumLength(100);
        RuleFor(filter => filter.Currency).IsInEnum().When(filter => filter.Currency.HasValue);
    }
}
