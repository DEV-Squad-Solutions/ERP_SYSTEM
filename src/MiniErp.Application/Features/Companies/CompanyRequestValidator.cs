using FluentValidation;

namespace MiniErp.Application.Features.Companies;

public sealed class CompanyRequestValidator : AbstractValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Address)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(request => request.CommercialRegister)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.TaxNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.ManagerName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
