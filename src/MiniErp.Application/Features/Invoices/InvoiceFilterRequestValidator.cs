using FluentValidation;

namespace MiniErp.Application.Features.Invoices;

public sealed class InvoiceFilterRequestValidator
    : AbstractValidator<InvoiceFilterRequest>
{
    public InvoiceFilterRequestValidator()
    {
        RuleFor(filter => filter.InvoiceNumber)
            .MaximumLength(InvoiceRequest.InvoiceNumberMaximumLength)
            .When(filter =>
                filter.InvoiceNumber is not null &&
                filter.InvoiceNumber.Trim().Length >
                InvoiceRequest.InvoiceNumberMaximumLength);

        RuleFor(filter => filter.InvoiceType)
            .IsInEnum()
            .When(filter => filter.InvoiceType.HasValue);

        RuleFor(filter => filter.PaymentTerm)
            .IsInEnum()
            .When(filter => filter.PaymentTerm.HasValue);

        RuleFor(filter => filter.PriceStatus)
            .IsInEnum()
            .When(filter => filter.PriceStatus.HasValue);

        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);

        RuleFor(filter => filter.CountryId)
            .GreaterThan(0)
            .When(filter => filter.CountryId.HasValue);

        RuleFor(filter => filter.StoreId)
            .GreaterThan(0)
            .When(filter => filter.StoreId.HasValue);

        RuleFor(filter => filter.DriverId)
            .GreaterThan(0)
            .When(filter => filter.DriverId.HasValue);

        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
