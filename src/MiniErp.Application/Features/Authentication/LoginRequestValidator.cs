using FluentValidation;

namespace MiniErp.Application.Features.Authentication;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.UserName)
            .NotEmpty();

        RuleFor(request => request.Password)
            .NotEmpty();
    }
}
