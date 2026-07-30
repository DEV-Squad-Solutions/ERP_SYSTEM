using FluentValidation;

namespace MiniErp.Application.Features.ItemsCategories;

public sealed class ItemsCategoryRequestValidator
    : AbstractValidator<ItemsCategoryRequest>
{
    public ItemsCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(ItemsCategoryRequest.NameMaximumLength);

        RuleFor(request => request.Notes)
            .MaximumLength(ItemsCategoryRequest.NotesMaximumLength);
    }
}

public sealed class ItemsCategoryUpdateRequestValidator
    : AbstractValidator<ItemsCategoryUpdateRequest>
{
    public ItemsCategoryUpdateRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(ItemsCategoryRequest.NameMaximumLength);

        RuleFor(request => request.Notes)
            .MaximumLength(ItemsCategoryRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار تصنيف الأصناف الحالي للتعديل.");
    }
}

public sealed class ItemsCategoryFilterRequestValidator
    : AbstractValidator<ItemsCategoryFilterRequest>
{
    public ItemsCategoryFilterRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(ItemsCategoryRequest.NotesMaximumLength);

        RuleFor(request => request.Name)
            .MaximumLength(ItemsCategoryRequest.NameMaximumLength);
    }
}
