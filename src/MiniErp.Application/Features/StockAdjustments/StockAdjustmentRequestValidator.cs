using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed class StockAdjustmentLineRequestValidator
    : AbstractValidator<StockAdjustmentLineRequest>
{
    public StockAdjustmentLineRequestValidator()
    {
        RuleFor(line => line.ItemId)
            .GreaterThan(0);

        RuleFor(line => line.Quantity)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryQuantityRules.Precision,
                InventoryQuantityRules.Scale,
                ignoreTrailingZeros: true);

        RuleFor(line => line.Reason)
            .MaximumLength(StockAdjustmentRequest.ReasonMaximumLength);
    }
}

public sealed class StockAdjustmentRequestValidator
    : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        AddCommonRules();
    }

    private void AddCommonRules()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentNumber)
            .NotEmpty()
            .MaximumLength(
                StockAdjustmentRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Reason)
            .MaximumLength(StockAdjustmentRequest.ReasonMaximumLength);

        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines =>
                lines is not null &&
                lines.Count <= StockAdjustmentRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور التسوية {StockAdjustmentRequest.MaximumLineCount}.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في التسوية مطلوب.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور التسوية.");

        RuleForEach(request => request.Lines)
            .SetValidator(new StockAdjustmentLineRequestValidator());
    }
}

public sealed class StockAdjustmentUpdateRequestValidator
    : AbstractValidator<StockAdjustmentUpdateRequest>
{
    public StockAdjustmentUpdateRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentNumber)
            .NotEmpty()
            .MaximumLength(
                StockAdjustmentRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Reason)
            .MaximumLength(StockAdjustmentRequest.ReasonMaximumLength);

        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines =>
                lines is not null &&
                lines.Count <= StockAdjustmentRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور التسوية {StockAdjustmentRequest.MaximumLineCount}.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في التسوية مطلوب.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور التسوية.");

        RuleForEach(request => request.Lines)
            .SetValidator(new StockAdjustmentLineRequestValidator());

        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.");
    }
}
