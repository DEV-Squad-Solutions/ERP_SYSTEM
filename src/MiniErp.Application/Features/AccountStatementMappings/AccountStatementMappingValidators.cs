using FluentValidation;

namespace MiniErp.Application.Features.AccountStatementMappings;

public sealed class ReplaceAccountStatementMappingsRequestValidator
    : AbstractValidator<ReplaceAccountStatementMappingsRequest>
{
    public ReplaceAccountStatementMappingsRequestValidator()
    {
        RuleFor(request => request.Mappings).NotNull();
        RuleForEach(request => request.Mappings)
            .SetValidator(new AccountStatementMappingRowRequestValidator());
    }
}

public sealed class AccountStatementMappingRowRequestValidator
    : AbstractValidator<AccountStatementMappingRowRequest>
{
    public AccountStatementMappingRowRequestValidator()
    {
        RuleFor(request => request.AccountId).GreaterThan(0);
        RuleFor(request => request.FinancialStatementLineId).GreaterThan(0);
    }
}
