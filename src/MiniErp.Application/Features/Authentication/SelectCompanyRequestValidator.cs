using FluentValidation;

namespace MiniErp.Application.Features.Authentication;

public sealed class SelectCompanyRequestValidator
    : AbstractValidator<SelectCompanyRequest>
{
    public SelectCompanyRequestValidator()
    {
        RuleFor(request => request.SelectionToken)
            .NotEmpty();

        RuleFor(request => request.CompanyId)
            .GreaterThan(0);
    }
}
