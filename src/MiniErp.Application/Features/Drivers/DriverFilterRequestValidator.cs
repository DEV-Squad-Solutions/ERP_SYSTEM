using FluentValidation;

namespace MiniErp.Application.Features.Drivers;

public sealed class DriverFilterRequestValidator : AbstractValidator<DriverFilterRequest>
{
    public DriverFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.Code).MaximumLength(50);
        RuleFor(filter => filter.Name).MaximumLength(200);
        RuleFor(filter => filter.LicenseNumber).MaximumLength(50);
        RuleFor(filter => filter.LicenseExpiryTo)
            .GreaterThanOrEqualTo(filter => filter.LicenseExpiryFrom)
            .When(filter => filter.LicenseExpiryFrom.HasValue && filter.LicenseExpiryTo.HasValue)
            .WithMessage("تاريخ انتهاء الرخصة النهائي يجب ألا يسبق التاريخ الابتدائي.");
    }
}
