using FluentValidation;

namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed class StockOpeningBalanceFilterRequestValidator
    : AbstractValidator<StockOpeningBalanceFilterRequest>
{
    public StockOpeningBalanceFilterRequestValidator()
    {
        RuleFor(filter => filter.DocumentNumber).MaximumLength(50);
        RuleFor(filter => filter.StoreId)
            .GreaterThan(0)
            .When(filter => filter.StoreId.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter => filter.FromDate.HasValue && filter.ToDate.HasValue)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
