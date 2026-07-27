using FluentValidation;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceFilterRequestValidator
    : AbstractValidator<PartnerOpeningBalanceFilterRequest>
{
    public PartnerOpeningBalanceFilterRequestValidator()
    {
        RuleFor(filter => filter.DocumentNumber).MaximumLength(50);
        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);
        RuleFor(filter => filter.Currency).IsInEnum().When(filter => filter.Currency.HasValue);
        RuleFor(filter => filter.BalanceType).IsInEnum().When(filter => filter.BalanceType.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter => filter.FromDate.HasValue && filter.ToDate.HasValue)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
