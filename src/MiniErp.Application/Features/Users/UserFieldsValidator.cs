using FluentValidation;

namespace MiniErp.Application.Features.Users;

internal sealed class UserFieldsValidator : AbstractValidator<UserCreateRequest>
{
    public UserFieldsValidator()
    {
        RuleFor(request => request.UserName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(request => request.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(50)
            .When(request => request.PhoneNumber is not null);

        RuleFor(request => request.Roles)
            .NotNull()
            .SetValidator(new RolesValidator());

        RuleFor(request => request.CompanyIds)
            .SetValidator(new CompanyIdsValidator());
    }
}
