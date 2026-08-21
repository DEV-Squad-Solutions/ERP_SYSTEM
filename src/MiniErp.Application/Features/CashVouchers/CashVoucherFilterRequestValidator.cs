using FluentValidation;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherFilterRequestValidator
    : AbstractValidator<CashVoucherFilterRequest>
{
    public CashVoucherFilterRequestValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.VoucherNumber)
            .MaximumLength(CashVoucherFilterRequest.VoucherNumberMaximumLength);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.CashboxId)
            .GreaterThan(0)
            .When(filter => filter.CashboxId.HasValue);
        RuleFor(filter => filter.CashMovementTypeId)
            .GreaterThan(0)
            .When(filter => filter.CashMovementTypeId.HasValue);
        RuleFor(filter => filter.Classification)
            .IsInEnum()
            .When(filter => filter.Classification.HasValue);
        RuleFor(filter => filter.PartyType)
            .IsInEnum()
            .When(filter => filter.PartyType.HasValue);
        RuleFor(filter => filter.EmployeeId)
            .GreaterThan(0)
            .When(filter => filter.EmployeeId.HasValue);
        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);
        RuleFor(filter => filter.DriverId)
            .GreaterThan(0)
            .When(filter => filter.DriverId.HasValue);
        RuleFor(filter => filter.DriverTripId)
            .GreaterThan(0)
            .When(filter => filter.DriverTripId.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
