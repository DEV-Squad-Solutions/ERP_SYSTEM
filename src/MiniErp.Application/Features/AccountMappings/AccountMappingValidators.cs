using FluentValidation;

namespace MiniErp.Application.Features.AccountMappings;

public sealed class ReplaceAccountMappingsRequestValidator
    : AbstractValidator<ReplaceAccountMappingsRequest>
{
    public ReplaceAccountMappingsRequestValidator()
    {
        RuleFor(request => request.Mappings).NotNull();
        RuleForEach(request => request.Mappings)
            .SetValidator(new AccountMappingRequestValidator());
    }
}

public sealed class AccountMappingRequestValidator
    : AbstractValidator<AccountMappingRequest>
{
    public AccountMappingRequestValidator()
    {
        RuleFor(request => request.AccountId).GreaterThan(0);
    }
}
