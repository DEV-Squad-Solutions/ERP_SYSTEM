using FluentValidation;

namespace MiniErp.Application.Features.DriverTrips;

public sealed class DriverTripCostUpdateItemValidator
    : AbstractValidator<DriverTripCostUpdateItem>
{
    public DriverTripCostUpdateItemValidator()
    {
        RuleFor(item => item.DriverTripId)
            .GreaterThan(0);

        RuleFor(item => item.Cost)
            .InclusiveBetween(0m, DriverTripRequest.MaximumCost)
            .PrecisionScale(17, 2, ignoreTrailingZeros: true)
            .When(item => item.Cost.HasValue);

        RuleFor(item => item.Notes)
            .MaximumLength(
                DriverTripBulkCostUpdateRequest.NotesMaximumLength);

        RuleFor(item => item.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار رحلة السائق الحالي للتعديل.");
    }
}

public sealed class DriverTripBulkCostUpdateRequestValidator
    : AbstractValidator<DriverTripBulkCostUpdateRequest>
{
    public DriverTripBulkCostUpdateRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotNull()
            .NotEmpty()
            .Must(items =>
                items is not null && items.Count <=
                DriverTripBulkCostUpdateRequest.MaximumItemCount)
            .WithMessage(
                $"لا يمكن تعديل أكثر من {DriverTripBulkCostUpdateRequest.MaximumItemCount} رحلة في طلب واحد.");

        RuleForEach(request => request.Items)
            .NotNull()
            .SetValidator(new DriverTripCostUpdateItemValidator());

        RuleFor(request => request.Items)
            .Must(items =>
                items.All(item => item is not null) &&
                items.Select(item => item.DriverTripId).Distinct().Count() == items.Count)
            .When(request => request.Items is { Count: > 0 })
            .WithMessage(
                "لا يجوز تكرار رقم رحلة السائق داخل الطلب.");
    }
}
