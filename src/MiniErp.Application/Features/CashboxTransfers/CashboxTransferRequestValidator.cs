using FluentValidation;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.CashboxTransfers;

public sealed class CashboxTransferRequestValidator
    : AbstractValidator<CashboxTransferRequest>
{
    public CashboxTransferRequestValidator()
    {
        AddCommonRules();
    }

    private void AddCommonRules()
    {
        RuleFor(request => request.TransferDate)
            .Must(date => date != default)
            .WithMessage("تاريخ التحويل مطلوب.");
        RuleFor(request => request.SourceCashboxId)
            .GreaterThan(0);
        RuleFor(request => request.DestinationCashboxId)
            .GreaterThan(0)
            .NotEqual(request => request.SourceCashboxId)
            .WithMessage("يجب اختيار خزنة مستلمة مختلفة عن الخزنة المصدر.");
        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(request => request.Description)
            .MaximumLength(CashboxTransferRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes)
            .MaximumLength(CashboxTransferRequest.NotesMaximumLength);
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");
        RuleFor(request => request.DestinationAmount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .When(request => request.DestinationAmount.HasValue);
        RuleFor(request => request.ConversionRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر التحويل أكبر من صفر.");
        RuleFor(request => request.DestinationExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف الوجهة أكبر من صفر.");
    }
}

public sealed class CashboxTransferUpdateRequestValidator
    : AbstractValidator<CashboxTransferUpdateRequest>
{
    public CashboxTransferUpdateRequestValidator()
    {
        RuleFor(request => request.TransferDate)
            .Must(date => date != default)
            .WithMessage("تاريخ التحويل مطلوب.");
        RuleFor(request => request.SourceCashboxId)
            .GreaterThan(0);
        RuleFor(request => request.DestinationCashboxId)
            .GreaterThan(0)
            .NotEqual(request => request.SourceCashboxId)
            .WithMessage("يجب اختيار خزنة مستلمة مختلفة عن الخزنة المصدر.");
        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(request => request.Description)
            .MaximumLength(CashboxTransferRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes)
            .MaximumLength(CashboxTransferRequest.NotesMaximumLength);
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");
        RuleFor(request => request.DestinationAmount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .When(request => request.DestinationAmount.HasValue);
        RuleFor(request => request.ConversionRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر التحويل أكبر من صفر.");
        RuleFor(request => request.DestinationExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف الوجهة أكبر من صفر.");
        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("أعد تحميل التحويل ثم حاول التعديل مرة أخرى.");
    }
}

public sealed class CashboxTransferFilterRequestValidator
    : AbstractValidator<CashboxTransferFilterRequest>
{
    public CashboxTransferFilterRequestValidator()
    {
        RuleFor(filter => filter.Search)
            .MaximumLength(100);
        RuleFor(filter => filter.SourceCashboxId)
            .GreaterThan(0)
            .When(filter => filter.SourceCashboxId.HasValue);
        RuleFor(filter => filter.DestinationCashboxId)
            .GreaterThan(0)
            .When(filter => filter.DestinationCashboxId.HasValue);
        RuleFor(filter => filter)
            .Must(filter =>
                !filter.FromDate.HasValue ||
                !filter.ToDate.HasValue ||
                filter.FromDate <= filter.ToDate)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
