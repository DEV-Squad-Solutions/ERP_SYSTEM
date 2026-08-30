using FluentValidation;

namespace MiniErp.Application.Features.PayrollReport;

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
