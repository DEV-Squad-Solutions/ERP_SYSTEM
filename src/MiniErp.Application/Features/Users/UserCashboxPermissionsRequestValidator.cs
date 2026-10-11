using FluentValidation;

namespace MiniErp.Application.Features.Users;

public sealed class UserCashboxPermissionsRequestValidator : AbstractValidator<UserCashboxPermissionsRequest>
{
    public UserCashboxPermissionsRequestValidator()
    {
        RuleFor(request => request.Permissions).NotNull();
        RuleForEach(request => request.Permissions).NotNull().ChildRules(grant =>
        {
            grant.RuleFor(permission => permission.CashboxId).GreaterThan(0);
            grant.RuleFor(permission => permission.CanView)
                .Must((permission, _) => permission.CanView || permission.CanExecute)
                .WithMessage("يجب اختيار صلاحية العرض أو التنفيذ لكل خزنة.");
        });
    }
}
