using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherDraftUpdateRequest(
    DateOnly VoucherDate,
    CashDirection Direction,
    int CashboxId,
    decimal Amount,
    byte[]? RowVersion,
    string? Description = null,
    string? Notes = null,
    string? ReferenceNumber = null,
    decimal? ExchangeRate = null);

public sealed class CashVoucherDraftUpdateRequestValidator
    : AbstractValidator<CashVoucherDraftUpdateRequest>
{
    public CashVoucherDraftUpdateRequestValidator()
    {
        RuleFor(request => request.VoucherDate).Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");
        RuleFor(request => request.Direction).IsInEnum();
        RuleFor(request => request.CashboxId).GreaterThan(0);
        RuleFor(request => request.Amount).GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(request => request.Description).MaximumLength(CashVoucherRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes).MaximumLength(CashVoucherRequest.NotesMaximumLength);
        RuleFor(request => request.ReferenceNumber).MaximumLength(CashVoucherRequest.ReferenceNumberMaximumLength);
        RuleFor(request => request.ExchangeRate)
            .PrecisionScale(28, 12, ignoreTrailingZeros: true)
            .Must(rate => !rate.HasValue || ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("سعر صرف سند النقدية يجب أن يكون أكبر من صفر وبحد أقصى 12 منزلة عشرية.")
            .When(request => request.ExchangeRate.HasValue);
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار سند النقدية الحالي للتعديل.");
    }
}
