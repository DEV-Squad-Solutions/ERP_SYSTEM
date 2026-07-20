using FluentValidation;

namespace MiniErp.Application.Features.Users;

public sealed class UserCompaniesRequestValidator : AbstractValidator<UserCompaniesRequest>
{
    public UserCompaniesRequestValidator()
    {
        RuleFor(request => request.CompanyIds)
            .SetValidator(new CompanyIdsValidator());
    }
}
