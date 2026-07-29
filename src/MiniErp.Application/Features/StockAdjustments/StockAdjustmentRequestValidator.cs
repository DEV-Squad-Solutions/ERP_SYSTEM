using FluentValidation;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;

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

        RuleFor(line => line.UnitCost)
            .GreaterThanOrEqualTo(0m)
            .When(line => line.UnitCost.HasValue)
            .PrecisionScale(
                InventoryCostRules.UnitCostPrecision,
                InventoryCostRules.UnitCostScale,
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

        RuleFor(request => request.Lines)
            .Must(lines => lines.All(line => line.UnitCost.HasValue))
            .When(request =>
                request.Direction == StockAdjustmentDirection.Increase)
            .WithMessage(
                "يجب إدخال تكلفة الوحدة لكل سطر عند زيادة المخزون.");

        RuleFor(request => request.Lines)
            .Must(lines => lines.All(line => !line.UnitCost.HasValue))
            .When(request =>
                request.Direction == StockAdjustmentDirection.Decrease)
            .WithMessage(
                "لا يجوز إدخال تكلفة الوحدة في تسوية الخصم؛ يستخدم الخادم متوسط التكلفة الحالي.");
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

        RuleFor(request => request.Lines)
            .Must(lines => lines.All(line => line.UnitCost.HasValue))
            .When(request =>
                request.Direction == StockAdjustmentDirection.Increase)
            .WithMessage(
                "يجب إدخال تكلفة الوحدة لكل سطر عند زيادة المخزون.");

        RuleFor(request => request.Lines)
            .Must(lines => lines.All(line => !line.UnitCost.HasValue))
            .When(request =>
                request.Direction == StockAdjustmentDirection.Decrease)
            .WithMessage(
                "لا يجوز إدخال تكلفة الوحدة في تسوية الخصم؛ يستخدم الخادم متوسط التكلفة الحالي.");

        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.");
    }
}
