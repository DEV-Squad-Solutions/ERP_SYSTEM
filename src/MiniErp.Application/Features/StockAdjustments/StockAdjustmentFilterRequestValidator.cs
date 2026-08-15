using FluentValidation;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed class StockAdjustmentFilterRequestValidator
    : AbstractValidator<StockAdjustmentFilterRequest>
{
    public StockAdjustmentFilterRequestValidator()
    {
        RuleFor(request => request.DocumentNumber)
            .MaximumLength(
                StockAdjustmentFilterRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.StoreId)
            .GreaterThan(0)
            .When(request => request.StoreId.HasValue);

        RuleFor(request => request.Direction)
            .IsInEnum()
            .When(request => request.Direction.HasValue);

        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage(
                "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");
    }
}
