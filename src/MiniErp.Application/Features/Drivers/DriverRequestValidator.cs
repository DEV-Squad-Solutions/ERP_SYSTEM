using FluentValidation;

namespace MiniErp.Application.Features.Drivers;

public sealed class DriverRequestValidator : AbstractValidator<DriverRequest>
{
    public DriverRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request =>
                request.Name is not null &&
                request.Name.Trim().Length > 200);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(50)
            .When(request =>
                request.PhoneNumber is not null &&
                request.PhoneNumber.Trim().Length > 50);

        RuleFor(request => request.NationalId)
            .MaximumLength(50)
            .When(request =>
                request.NationalId is not null &&
                request.NationalId.Trim().Length > 50);

        RuleFor(request => request.LicenseNumber)
            .NotEmpty();

        RuleFor(request => request.LicenseNumber)
            .MaximumLength(100)
            .When(request =>
                request.LicenseNumber is not null &&
                request.LicenseNumber.Trim().Length > 100);
    }
}
