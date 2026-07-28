using FluentValidation;

namespace MiniErp.Application.Features.Users;

internal sealed class RolesValidator : AbstractValidator<IReadOnlyCollection<string>>
{
    public RolesValidator()
    {
        RuleFor(roles => roles)
            .NotNull()
            .NotEmpty();

        RuleForEach(roles => roles)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(roles => roles)
            .Must(roles => roles.All(role => role is not null) &&
                roles.Count == roles
                    .Select(role => role.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count())
            .When(roles => roles is not null)
            .WithMessage("يجب ألا تحتوي الأدوار على قيم مكررة.");
    }
}
