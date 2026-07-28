using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.InventoryCounts;

public sealed class InventoryCountRequestValidator
    : AbstractValidator<InventoryCountRequest>
{
    public InventoryCountRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentNumber)
            .NotEmpty()
            .MaximumLength(
                InventoryCountRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.CountDate)
            .Must(date => date != default)
            .WithMessage("تاريخ الجرد مطلوب.");

        RuleFor(request => request.Notes)
            .MaximumLength(InventoryCountRequest.NotesMaximumLength);
    }
}

public sealed class InventoryCountLineUpdateRequestValidator
    : AbstractValidator<InventoryCountLineUpdateRequest>
{
    public InventoryCountLineUpdateRequestValidator()
    {
        RuleFor(line => line.ItemId)
            .GreaterThan(0);

        RuleFor(line => line.PhysicalQuantity)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(
                InventoryQuantityRules.Precision,
                InventoryQuantityRules.Scale,
                ignoreTrailingZeros: true)
            .When(line => line.PhysicalQuantity.HasValue);

        RuleFor(line => line.Notes)
            .MaximumLength(InventoryCountRequest.NotesMaximumLength);
    }
}

public sealed class InventoryCountUpdateRequestValidator
    : AbstractValidator<InventoryCountUpdateRequest>
{
    public InventoryCountUpdateRequestValidator()
    {
        RuleFor(request => request.Notes)
            .MaximumLength(InventoryCountRequest.NotesMaximumLength);

        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في مستند الجرد مطلوب.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في مستند الجرد.");

        RuleForEach(request => request.Lines)
            .SetValidator(new InventoryCountLineUpdateRequestValidator());

        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.");
    }
}

public sealed class InventoryCountReconcileRequestValidator
    : AbstractValidator<InventoryCountReconcileRequest>
{
    public InventoryCountReconcileRequestValidator()
    {
        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتسوية.");
    }
}
