using FluentValidation;

namespace MiniErp.Application.Features.Authentication;

public sealed class SwitchCompanyRequestValidator
    : AbstractValidator<SwitchCompanyRequest>
{
    public SwitchCompanyRequestValidator()
    {
        RuleFor(request => request.CompanyId)
            .GreaterThan(0);

        RuleFor(request => request.RefreshToken)
            .NotEmpty();
    }
}
