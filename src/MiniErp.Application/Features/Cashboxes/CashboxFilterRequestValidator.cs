using FluentValidation;

namespace MiniErp.Application.Features.Cashboxes;

public sealed class CashboxFilterRequestValidator
    : AbstractValidator<CashboxFilterRequest>
{
    public CashboxFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.Code)
            .MaximumLength(CashboxRequest.CodeMaximumLength);
        RuleFor(filter => filter.Name)
            .MaximumLength(CashboxRequest.NameMaximumLength);
        RuleFor(filter => filter.Currency)
            .IsInEnum()
            .When(filter => filter.Currency.HasValue);
    }
}
