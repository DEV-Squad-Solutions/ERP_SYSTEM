using FluentValidation;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed class PayrollEntrySalaryPaymentRequestValidator
    : AbstractValidator<PayrollEntrySalaryPaymentRequest>
{
    public PayrollEntrySalaryPaymentRequestValidator()
    {
        RuleFor(x => x.PostingDate)
            .NotEmpty()
            .WithMessage("يجب تحديد تاريخ القيد.");
    }
}
