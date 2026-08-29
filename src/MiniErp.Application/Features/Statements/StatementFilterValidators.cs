using FluentValidation;

namespace MiniErp.Application.Features.Statements;

public sealed class CashboxStatementFilterRequestValidator
    : AbstractValidator<CashboxStatementFilterRequest>
{
    public CashboxStatementFilterRequestValidator()
    {
        RuleFor(filter => filter.CashboxId).GreaterThan(0);
        AddCommonRules(this);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.CashMovementTypeId)
            .GreaterThan(0)
            .When(filter => filter.CashMovementTypeId.HasValue);
        RuleFor(filter => filter.Classification)
            .IsInEnum()
            .When(filter => filter.Classification.HasValue);
        RuleFor(filter => filter.PartyType)
            .IsInEnum()
            .When(filter => filter.PartyType.HasValue);
        RuleFor(filter => filter.BusinessPartnerId)
            .GreaterThan(0)
            .When(filter => filter.BusinessPartnerId.HasValue);
        RuleFor(filter => filter.DriverId)
            .GreaterThan(0)
            .When(filter => filter.DriverId.HasValue);
        RuleFor(filter => filter.DriverTripId)
            .GreaterThan(0)
            .When(filter => filter.DriverTripId.HasValue);
        RuleFor(filter => filter.EmployeeId)
            .GreaterThan(0)
            .When(filter => filter.EmployeeId.HasValue);
        RuleFor(filter => filter.VoucherNumber).MaximumLength(100);
    }

    private static void AddCommonRules(
        AbstractValidator<CashboxStatementFilterRequest> validator)
    {
        validator.RuleFor(filter => filter.Search).MaximumLength(256);
        validator.RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}

public sealed class PartnerStatementFilterRequestValidator
    : AbstractValidator<PartnerStatementFilterRequest>
{
    public PartnerStatementFilterRequestValidator()
    {
        RuleFor(filter => filter.BusinessPartnerId).GreaterThan(0);
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.SourceType)
            .IsInEnum()
            .When(filter => filter.SourceType.HasValue);
        RuleFor(filter => filter.MovementType)
            .IsInEnum()
            .When(filter => filter.MovementType.HasValue);
        RuleFor(filter => filter.CashMovementTypeId)
            .GreaterThan(0)
            .When(filter => filter.CashMovementTypeId.HasValue);
        RuleFor(filter => filter.Classification)
            .IsInEnum()
            .When(filter => filter.Classification.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}

public sealed class DriverStatementFilterRequestValidator
    : AbstractValidator<DriverStatementFilterRequest>
{
    public DriverStatementFilterRequestValidator()
    {
        RuleFor(filter => filter.DriverId).GreaterThan(0);
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.CashMovementTypeId)
            .GreaterThan(0)
            .When(filter => filter.CashMovementTypeId.HasValue);
        RuleFor(filter => filter.Classification)
            .IsInEnum()
            .When(filter => filter.Classification.HasValue);
        RuleFor(filter => filter.DriverTripId)
            .GreaterThan(0)
            .When(filter => filter.DriverTripId.HasValue);
        RuleFor(filter => filter.InvoiceNumber).MaximumLength(100);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}

public sealed class ContainerStoreStatementFilterRequestValidator
    : AbstractValidator<ContainerStoreStatementFilterRequest>
{
    public ContainerStoreStatementFilterRequestValidator()
    {
        RuleFor(filter => filter.BusinessPartnerId).GreaterThan(0);
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.ContainerId)
            .GreaterThan(0)
            .When(filter => filter.ContainerId.HasValue);
        RuleFor(filter => filter.InvoiceType)
            .IsInEnum()
            .When(filter => filter.InvoiceType.HasValue);
        RuleFor(filter => filter.InvoiceNumber).MaximumLength(100);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}

public sealed class EmployeeStatementFilterRequestValidator
    : AbstractValidator<EmployeeStatementFilterRequest>
{
    public EmployeeStatementFilterRequestValidator()
    {
        RuleFor(filter => filter.EmployeeId).GreaterThan(0);
        RuleFor(filter => filter.Search).MaximumLength(256);
        RuleFor(filter => filter.SourceType)
            .IsInEnum()
            .When(filter => filter.SourceType.HasValue);
        RuleFor(filter => filter.MovementType)
            .IsInEnum()
            .When(filter => filter.MovementType.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter =>
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue)
            .WithMessage(
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
