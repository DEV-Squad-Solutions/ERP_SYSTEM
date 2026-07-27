using FluentValidation;

namespace MiniErp.Application.Features.Companies;

public sealed class CompanyFilterRequestValidator : AbstractValidator<CompanyFilterRequest>
{
    public CompanyFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(500);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.Address).MaximumLength(500);
        RuleFor(filter => filter.CommercialRegister).MaximumLength(50);
        RuleFor(filter => filter.TaxNumber).MaximumLength(50);
        RuleFor(filter => filter.ManagerName).MaximumLength(200);
    }
}
