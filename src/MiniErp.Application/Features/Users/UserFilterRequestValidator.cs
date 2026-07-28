using FluentValidation;

namespace MiniErp.Application.Features.Users;

public sealed class UserFilterRequestValidator : AbstractValidator<UserFilterRequest>
{
    public UserFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.UserName).MaximumLength(256);
        RuleFor(filter => filter.Email).MaximumLength(256);
        RuleFor(filter => filter.FirstName).MaximumLength(100);
        RuleFor(filter => filter.LastName).MaximumLength(100);
    }
}
