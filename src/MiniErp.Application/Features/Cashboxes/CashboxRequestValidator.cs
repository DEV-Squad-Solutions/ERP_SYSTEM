using FluentValidation;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.Cashboxes;

public sealed class CashboxRequestValidator : AbstractValidator<CashboxRequest>
{
    public CashboxRequestValidator()
    {
        AddRules();
    }

    private void AddRules()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(CashboxRequest.CodeMaximumLength);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashboxRequest.NameMaximumLength);

        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.OpeningBalance)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.OpeningExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف الرصيد الافتتاحي أكبر من صفر.");

        RuleFor(request => request.Notes)
            .MaximumLength(CashboxRequest.NotesMaximumLength);
    }
}

public sealed class CashboxUpdateRequestValidator
    : AbstractValidator<CashboxUpdateRequest>
{
    public CashboxUpdateRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(CashboxRequest.CodeMaximumLength);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashboxRequest.NameMaximumLength);

        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.OpeningBalance)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.OpeningExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف الرصيد الافتتاحي أكبر من صفر.");

        RuleFor(request => request.Notes)
            .MaximumLength(CashboxRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار صندوق النقدية الحالي للتعديل.");
    }
}
