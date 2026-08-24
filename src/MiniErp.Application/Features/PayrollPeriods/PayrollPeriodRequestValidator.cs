using FluentValidation;

namespace MiniErp.Application.Features.PayrollPeriods;

public sealed class PayrollPeriodCreateRequestValidator
    : AbstractValidator<PayrollPeriodCreateRequest>
{
    public PayrollPeriodCreateRequestValidator()
    {
        RuleFor(r => r.StartDate)
            .NotEmpty()
            .WithMessage("تاريخ البدء مطلوب.");

        RuleFor(r => r.EndDate)
            .NotEmpty()
            .WithMessage("تاريخ الانتهاء مطلوب.");

        RuleFor(r => r)
            .Must(r => r.StartDate <= r.EndDate)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء.");

        RuleFor(r => r.WorkingDaysInPeriod)
            .GreaterThan(0)
            .WithMessage("أيام العمل في الفترة يجب أن تكون أكبر من صفر.");

        RuleFor(r => r.Name)
            .MaximumLength(100)
            .When(r => r.Name is not null)
            .WithMessage("اسم الفترة لا يمكن أن يتجاوز 100 حرف.");
    }
}

public sealed class PayrollPeriodUpdateRequestValidator
    : AbstractValidator<PayrollPeriodUpdateRequest>
{
    public PayrollPeriodUpdateRequestValidator()
    {
        RuleFor(r => r.StartDate)
            .NotEmpty()
            .WithMessage("تاريخ البدء مطلوب.");

        RuleFor(r => r.EndDate)
            .NotEmpty()
            .WithMessage("تاريخ الانتهاء مطلوب.");

        RuleFor(r => r)
            .Must(r => r.StartDate <= r.EndDate)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء.");

        RuleFor(r => r.WorkingDaysInPeriod)
            .GreaterThan(0)
            .WithMessage("أيام العمل في الفترة يجب أن تكون أكبر من صفر.");

        RuleFor(r => r.Name)
            .MaximumLength(100)
            .When(r => r.Name is not null)
            .WithMessage("اسم الفترة لا يمكن أن يتجاوز 100 حرف.");
    }
}

public sealed class PayrollPeriodReportByDateRangeRequestValidator
    : AbstractValidator<PayrollPeriodReportByDateRangeRequest>
{
    public PayrollPeriodReportByDateRangeRequestValidator()
    {
        RuleFor(r => r.StartDate)
            .NotEmpty()
            .WithMessage("تاريخ البدء مطلوب.");

        RuleFor(r => r.EndDate)
            .NotEmpty()
            .WithMessage("تاريخ الانتهاء مطلوب.");

        RuleFor(r => r)
            .Must(r => r.StartDate <= r.EndDate)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء.");
    }
}
