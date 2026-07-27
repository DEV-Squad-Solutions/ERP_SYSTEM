using FluentValidation;

namespace MiniErp.Application.Features.DriverTrips;

public sealed class DriverTripCostFilterRequestValidator
    : AbstractValidator<DriverTripCostFilterRequest>
{
    public DriverTripCostFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.DriverId)
            .GreaterThan(0)
            .When(filter => filter.DriverId.HasValue);
        RuleFor(filter => filter.InvoiceNumber).MaximumLength(100);
        RuleFor(filter => filter.TripNumber).MaximumLength(50);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
