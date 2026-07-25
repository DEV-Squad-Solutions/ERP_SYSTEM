using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed class StockOpeningBalanceLineRequestValidator
    : AbstractValidator<StockOpeningBalanceLineRequest>
{
    public StockOpeningBalanceLineRequestValidator()
    {
        RuleFor(line => line.ItemId)
            .GreaterThan(0);

        RuleFor(line => line.Count)
            .GreaterThan(0);

        RuleFor(line => line.Weight)
            .GreaterThan(0)
            .PrecisionScale(
                StockOpeningBalanceAmountRules.QuantityPrecision,
                StockOpeningBalanceAmountRules.QuantityScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line.Price)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(
                StockOpeningBalanceAmountRules.MoneyPrecision,
                StockOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line)
            .Must(line => StockOpeningBalanceAmountRules.TryCalculate(
                line.Count,
                line.Weight,
                line.Price,
                out _,
                out _))
            .WithMessage(
                "ناتج الكمية أو الإجمالي يتجاوز الدقة الرقمية المسموح بها.");

        RuleFor(line => line.Notes)
            .MaximumLength(StockOpeningBalanceRequest.NotesMaximumLength);
    }
}

public sealed class StockOpeningBalanceRequestValidator
    : AbstractValidator<StockOpeningBalanceRequest>
{
    public StockOpeningBalanceRequestValidator()
    {
        StockOpeningBalanceValidationRules.Add(this);
    }
}

public sealed class StockOpeningBalanceUpdateRequestValidator
    : AbstractValidator<StockOpeningBalanceUpdateRequest>
{
    public StockOpeningBalanceUpdateRequestValidator()
    {
        StockOpeningBalanceValidationRules.Add(this);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: > 0 })
            .WithMessage("يجب إرسال إصدار السجل الحالي للتعديل.");
    }
}

internal static class StockOpeningBalanceValidationRules
{
    public static void Add<T>(AbstractValidator<T> validator)
        where T : IStockOpeningBalanceRequest
    {
        validator.RuleFor(request => request.StoreId)
            .GreaterThan(0);

        validator.RuleFor(request => request.DocumentNumber)
            .NotEmpty()
            .Must(number => !string.IsNullOrWhiteSpace(number))
            .WithMessage("رقم المستند مطلوب.")
            .MaximumLength(StockOpeningBalanceRequest.DocumentNumberMaximumLength);

        validator.RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        validator.RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines => lines is not null &&
                lines.Count <= StockOpeningBalanceRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور الرصيد الافتتاحي {StockOpeningBalanceRequest.MaximumLineCount}.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في المستند مطلوب.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() == lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور الرصيد الافتتاحي.");

        validator.RuleFor(request => request.Notes)
            .MaximumLength(StockOpeningBalanceRequest.NotesMaximumLength);

        validator.RuleForEach(request => request.Lines)
            .SetValidator(new StockOpeningBalanceLineRequestValidator());
    }
}
