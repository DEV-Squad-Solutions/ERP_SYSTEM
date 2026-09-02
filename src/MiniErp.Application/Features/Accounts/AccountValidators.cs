using FluentValidation;

namespace MiniErp.Application.Features.Accounts;

public sealed class AccountRequestValidator : AbstractValidator<AccountRequest>
{
    public AccountRequestValidator()
    {
        Include(new AccountFieldsValidator());
    }
}

public sealed class AccountUpdateRequestValidator
    : AbstractValidator<AccountUpdateRequest>
{
    public AccountUpdateRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(AccountRequest.CodeMaximumLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(AccountRequest.NameMaximumLength);
        RuleFor(request => request.ParentAccountId)
            .GreaterThan(0)
            .When(request => request.ParentAccountId.HasValue);
        RuleFor(request => request.AccountType).IsInEnum();
        RuleFor(request => request.NormalBalance).IsInEnum();
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار الحساب الحالي للتعديل.");
    }
}

public sealed class AccountFilterRequestValidator
    : AbstractValidator<AccountFilterRequest>
{
    public AccountFilterRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(AccountRequest.NameMaximumLength);
        RuleFor(request => request.AccountType!.Value)
            .IsInEnum()
            .When(request => request.AccountType.HasValue);
        RuleFor(request => request.NormalBalance!.Value)
            .IsInEnum()
            .When(request => request.NormalBalance.HasValue);
        RuleFor(request => request.ParentAccountId)
            .GreaterThan(0)
            .When(request => request.ParentAccountId.HasValue);
    }
}

internal sealed class AccountFieldsValidator : AbstractValidator<AccountRequest>
{
    public AccountFieldsValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(AccountRequest.CodeMaximumLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(AccountRequest.NameMaximumLength);
        RuleFor(request => request.ParentAccountId)
            .GreaterThan(0)
            .When(request => request.ParentAccountId.HasValue);
        RuleFor(request => request.AccountType).IsInEnum();
        RuleFor(request => request.NormalBalance).IsInEnum();
    }
}
