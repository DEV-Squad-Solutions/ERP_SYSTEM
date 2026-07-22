using FluentValidation;

namespace MiniErp.Application.Features.Users;

internal sealed class CompanyIdsValidator : AbstractValidator<IReadOnlyCollection<int>>
{
    public CompanyIdsValidator()
    {
        RuleFor(companyIds => companyIds)
            .NotNull()
            .NotEmpty();

        RuleForEach(companyIds => companyIds)
            .GreaterThan(0);

        RuleFor(companyIds => companyIds)
            .Must(companyIds => companyIds.Count == companyIds.Distinct().Count())
            .When(companyIds => companyIds is not null)
            .WithMessage("يجب ألا تحتوي الشركات على قيم مكررة.");
    }
}
