using FluentValidation;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed class BusinessPartnerRequestValidator
    : AbstractValidator<BusinessPartnerRequest>
{
    public BusinessPartnerRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(50);

        RuleFor(request => request.Email)
            .MaximumLength(256)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Address)
            .MaximumLength(500);

        RuleFor(request => request.TaxNumber)
            .MaximumLength(100);

        RuleFor(request => request.Currency)
            .IsInEnum();

        RuleFor(request => request.CreditLimit)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, false);
    }
}
