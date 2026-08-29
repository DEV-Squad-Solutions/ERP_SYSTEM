using FluentValidation;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed class EmployeeOpeningBalanceFilterRequestValidator
    : AbstractValidator<EmployeeOpeningBalanceFilterRequest>
{
    public EmployeeOpeningBalanceFilterRequestValidator()
    {
        RuleFor(filter => filter.EmployeeId)
            .GreaterThan(0)
            .When(filter => filter.EmployeeId.HasValue)
            .WithMessage("معرف الموظف غير صالح.");

        RuleFor(filter => filter.PayrollEntryId)
            .GreaterThan(0)
            .When(filter => filter.PayrollEntryId.HasValue)
            .WithMessage("معرف قيد الرواتب غير صالح.");

        RuleFor(filter => filter.Currency)
            .IsInEnum()
            .When(filter => filter.Currency.HasValue)
            .WithMessage("العملة المحددة غير صالحة.");

        RuleFor(filter => filter.BalanceType)
            .IsInEnum()
            .When(filter => filter.BalanceType.HasValue)
            .WithMessage("نوع الرصيد المحدد غير صالح.");

        RuleFor(filter => filter)
            .Must(filter =>
                !filter.FromDate.HasValue ||
                !filter.ToDate.HasValue ||
                filter.FromDate.Value <= filter.ToDate.Value)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ النهاية.");
    }
}
