using FluentValidation;

namespace MiniErp.Application.Features.DriverTrips;

public class DriverTripRequestValidator : AbstractValidator<DriverTripRequest>
{
    public DriverTripRequestValidator()
    {
        RuleFor(row => row.DriverId).GreaterThan(0);
        RuleFor(row => row.FiscalYearId).GreaterThan(0);
        RuleFor(row => row.TripDate).NotEmpty();
        RuleFor(row => row.BusinessPartnerId).GreaterThan(0).When(row => row.BusinessPartnerId.HasValue);
        RuleFor(row => row.Cost).InclusiveBetween(0m, DriverTripRequest.MaximumCost)
            .PrecisionScale(17, 2, ignoreTrailingZeros: true).When(row => row.Cost.HasValue);
        RuleFor(row => row.Notes).MaximumLength(DriverTripRequest.MaximumNotesLength);
        RuleFor(row => row.ActualDriverName).MaximumLength(DriverTripRequest.MaximumActualDriverNameLength);
    }
}

public sealed class DriverTripUpdateRequestValidator : AbstractValidator<DriverTripUpdateRequest>
{
    public DriverTripUpdateRequestValidator()
    {
        Include(new DriverTripRequestValidator());
        RuleFor(row => row.RowVersion).NotNull().Must(version => version is { Length: 8 });
    }
}
