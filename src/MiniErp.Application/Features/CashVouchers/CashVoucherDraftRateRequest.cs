using FluentValidation;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherDraftRateRequest(
    decimal ExchangeRate,
    byte[]? RowVersion);

public sealed class CashVoucherDraftRateRequestValidator
    : AbstractValidator<CashVoucherDraftRateRequest>
{
    public CashVoucherDraftRateRequestValidator()
    {
        RuleFor(request => request.ExchangeRate)
            .PrecisionScale(28, 12, ignoreTrailingZeros: true)
            .Must(ExchangeRateRules.IsValidRate)
            .WithMessage("سعر صرف سند النقدية يجب أن يكون أكبر من صفر وبحد أقصى 12 منزلة عشرية.");
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار سند النقدية الحالي للتعديل.");
    }
}
