using FluentValidation;

namespace MiniErp.Application.Features.Drivers;

public sealed class DriverRequestValidator : AbstractValidator<DriverRequest>
{
    public DriverRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(50);

        RuleFor(request => request.NationalId)
            .MaximumLength(50);

        RuleFor(request => request.LicenseNumber)
            .NotEmpty()
            .MaximumLength(100);
    }
}
