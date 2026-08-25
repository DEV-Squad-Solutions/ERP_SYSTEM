using FluentValidation;
using System.Linq;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed class IndividualPayrollEntryCreateRequestValidator
    : AbstractValidator<IndividualPayrollEntryCreateRequest>
{
    public IndividualPayrollEntryCreateRequestValidator()
    {
        RuleFor(item => item.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صالح.");

        RuleFor(item => item.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(item => item.Bonus.HasValue)
            .WithMessage("المكافأة يجب أن تكون أكبر من أو تساوي صفر.");

        RuleFor(item => item.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(item => item.Deduction.HasValue)
            .WithMessage("الخصم يجب أن يكون أكبر من أو تساوي صفر.");

        RuleFor(item => item)
            .Must(item => !item.StartDate.HasValue || !item.EndDate.HasValue || item.StartDate.Value <= item.EndDate.Value)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء.");
    }
}

public sealed class BulkPayrollEntryCreateRequestValidator
    : AbstractValidator<BulkPayrollEntryCreateRequest>
{
    public const int MaximumItemCount = 1000;

    public BulkPayrollEntryCreateRequestValidator()
    {
        RuleFor(request => request.Entries)
            .NotNull()
            .NotEmpty()
            .WithMessage("يجب إرسال مدخل راتب واحد على الأقل.");

        RuleFor(request => request.Entries)
            .Must(items => items.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن إنشاء قيود رواتب لأكثر من {MaximumItemCount} موظف في طلب واحد.")
            .When(request => request.Entries is not null);

        RuleForEach(request => request.Entries)
            .SetValidator(new IndividualPayrollEntryCreateRequestValidator())
            .When(request => request.Entries is not null);

        RuleFor(request => request.Entries)
            .Must(items => items.Select(item => item.EmployeeId).Distinct().Count() == items.Count)
            .When(request => request.Entries is { Count: > 0 })
            .WithMessage("لا يجوز تكرار نفس الموظف داخل الطلب الواحد.");

        RuleFor(request => request)
            .Must(request => !request.DefaultStartDate.HasValue || !request.DefaultEndDate.HasValue || request.DefaultStartDate.Value <= request.DefaultEndDate.Value)
            .WithMessage("تاريخ البدء الافتراضي يجب أن يكون قبل أو يساوي تاريخ الانتهاء الافتراضي.");
    }
}

public sealed class IndividualPayrollEntrySalaryPaymentRequestValidator
    : AbstractValidator<IndividualPayrollEntrySalaryPaymentRequest>
{
    public IndividualPayrollEntrySalaryPaymentRequestValidator()
    {
        RuleFor(item => item.PayrollEntryId)
            .GreaterThan(0)
            .WithMessage("معرف قيد الراتب غير صالح.");

        RuleFor(item => item.Notes)
            .MaximumLength(500)
            .WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف.");
    }
}

public sealed class BulkPayrollEntrySalaryPaymentRequestValidator
    : AbstractValidator<BulkPayrollEntrySalaryPaymentRequest>
{
    public const int MaximumItemCount = 1000;

    public BulkPayrollEntrySalaryPaymentRequestValidator()
    {
        RuleFor(request => request)
            .Must(r => (r.Entries != null && r.Entries.Count > 0) || (r.PayrollEntryIds != null && r.PayrollEntryIds.Count > 0))
            .WithMessage("يجب تحديد قيود الرواتب المطلوب صرفها.");

        RuleFor(request => request.Entries)
            .Must(items => items!.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن صرف رواتب أكثر من {MaximumItemCount} قيد في طلب واحد.")
            .When(request => request.Entries is not null);

        RuleFor(request => request.PayrollEntryIds)
            .Must(items => items!.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن صرف رواتب أكثر من {MaximumItemCount} قيد في طلب واحد.")
            .When(request => request.PayrollEntryIds is not null);

        RuleForEach(request => request.Entries)
            .SetValidator(new IndividualPayrollEntrySalaryPaymentRequestValidator())
            .When(request => request.Entries is not null);

        RuleForEach(request => request.PayrollEntryIds)
            .GreaterThan(0)
            .WithMessage("معرف قيد الراتب غير صالح.")
            .When(request => request.PayrollEntryIds is not null);

        RuleFor(request => request.Entries)
            .Must(items => items!.Select(item => item.PayrollEntryId).Distinct().Count() == items!.Count)
            .When(request => request.Entries is { Count: > 0 })
            .WithMessage("لا يجوز تكرار قيد الراتب داخل الطلب الواحد.");

        RuleFor(request => request.PayrollEntryIds)
            .Must(items => items!.Distinct().Count() == items!.Count)
            .When(request => request.PayrollEntryIds is { Count: > 0 })
            .WithMessage("لا يجوز تكرار قيد الراتب داخل الطلب الواحد.");
    }
}

public sealed class PayrollEntryUpdateRequestValidator
    : AbstractValidator<PayrollEntryUpdateRequest>
{
    public PayrollEntryUpdateRequestValidator()
    {
        RuleFor(r => r.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صالح.");

        RuleFor(r => r.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(r => r.Bonus.HasValue)
            .WithMessage("المكافأة يجب أن تكون أكبر من أو تساوي صفر.");

        RuleFor(r => r.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(r => r.Deduction.HasValue)
            .WithMessage("الخصم يجب أن يكون أكبر من أو يساوي صفر.");

        RuleFor(r => r)
            .Must(r => !r.StartDate.HasValue || !r.EndDate.HasValue || r.StartDate.Value <= r.EndDate.Value)
            .WithMessage("تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء.");
    }
}
