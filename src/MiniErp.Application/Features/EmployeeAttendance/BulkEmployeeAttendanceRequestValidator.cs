using FluentValidation;
using System;
using System.Linq;

namespace MiniErp.Application.Features.EmployeeAttendance;

public sealed class IndividualAttendanceRecordRequestValidator
    : AbstractValidator<IndividualAttendanceRecordRequest>
{
    public IndividualAttendanceRecordRequestValidator()
    {
        RuleFor(item => item.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صالح.");

        RuleFor(item => item.Status)
            .IsInEnum()
            .WithMessage("حالة الحضور غير صالحة.");

        RuleFor(item => item.WorkDate)
            .NotEmpty()
            .WithMessage("تاريخ العمل مطلوب.");

        RuleFor(item => item.CheckIn)
            .NotEmpty()
            .When(item => item.Status == Domain.Enums.EmployeeAttendanceStatus.Present)
            .WithMessage("وقت الحضور مطلوب عندما يكون الموظف حاضراً.");

        RuleFor(item => item.CheckOut)
            .NotEmpty()
            .When(item => item.Status == Domain.Enums.EmployeeAttendanceStatus.Present)
            .WithMessage("وقت الانصراف مطلوب عندما يكون الموظف حاضراً.");

        RuleFor(item => item.WorkDayRatio)
            .IsInEnum()
            .WithMessage("نسبة يوم العمل غير صالحة.");

        RuleFor(item => item.WorkOverTimeRatio)
            .IsInEnum()
            .When(item => item.WorkOverTimeRatio.HasValue)
            .WithMessage("نسبة العمل الإضافي غير صالحة.");

        RuleFor(item => item.WorkDaysDeductionRatio)
            .IsInEnum()
            .When(item => item.WorkDaysDeductionRatio.HasValue)
            .WithMessage("نسبة خصم أيام العمل غير صالحة.");

        RuleFor(item => item.WorkLocation)
            .MaximumLength(200)
            .WithMessage("موقع العمل يجب ألا يتجاوز 200 حرف.");

        RuleFor(item => item.Notes)
            .MaximumLength(500)
            .WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف.");
    }
}

public sealed class BulkEmployeeAttendanceRequestValidator
    : AbstractValidator<BulkEmployeeAttendanceRequest>
{
    public const int MaximumItemCount = 1000;

    public BulkEmployeeAttendanceRequestValidator()
    {
        RuleFor(request => request.Attendances)
            .NotNull()
            .NotEmpty()
            .WithMessage("يجب إرسال سجل حضور واحد على الأقل.");

        RuleFor(request => request.Attendances)
            .Must(items => items.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن تسجيل حضور أكثر من {MaximumItemCount} موظف في طلب واحد.")
            .When(request => request.Attendances is not null);

        RuleForEach(request => request.Attendances)
            .SetValidator(new IndividualAttendanceRecordRequestValidator())
            .When(request => request.Attendances is not null);

        RuleFor(request => request.Attendances)
            .Must(items =>
                items.Select(item => new { item.EmployeeId, item.WorkDate }).Distinct().Count() ==
                items.Count)
            .When(request => request.Attendances is { Count: > 0 })
            .WithMessage("لا يجوز تكرار تسجيل حضور نفس الموظف في نفس اليوم داخل الطلب.");
    }
}
