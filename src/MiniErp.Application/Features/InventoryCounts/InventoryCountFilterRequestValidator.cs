using FluentValidation;

namespace MiniErp.Application.Features.InventoryCounts;

public sealed class InventoryCountFilterRequestValidator
    : AbstractValidator<InventoryCountFilterRequest>
{
    public InventoryCountFilterRequestValidator()
    {
        RuleFor(request => request.DocumentNumber)
            .MaximumLength(
                InventoryCountFilterRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.StoreId)
            .GreaterThan(0)
            .When(request => request.StoreId.HasValue);

        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage(
                "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");
    }
}
