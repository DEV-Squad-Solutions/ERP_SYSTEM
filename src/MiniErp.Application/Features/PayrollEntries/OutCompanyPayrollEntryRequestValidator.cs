using FluentValidation;
using System.Linq;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed class OutCompanyPayrollEntryRequestValidator : AbstractValidator<OutCompanyPayrollEntryRequest>
{
    public OutCompanyPayrollEntryRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("يجب تحديد الموظف.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("تاريخ البداية مطلوب.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("تاريخ النهاية مطلوب.");

        RuleFor(x => x)
            .Must(x => x.StartDate <= x.EndDate)
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.StartDate != default && x.EndDate != default);

        RuleFor(x => x.PresentDays)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن يكون عدد أيام الحضور صفرًا أو أكبر.");

        RuleFor(x => x)
            .Must(x => x.PresentDays <= (x.EndDate.DayNumber - x.StartDate.DayNumber + 1))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة.")
            .When(x => x.StartDate != default && x.EndDate != default && x.StartDate <= x.EndDate);

        RuleFor(x => x.WorkedDaysByDayUnit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن تكون وحدات العمل صفرًا أو أكبر.");

        RuleFor(x => x.OvertimeByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.OvertimeByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات العمل الإضافي صفرًا أو أكبر.");

        RuleFor(x => x.DeductionByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DeductionByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات الخصم صفرًا أو أكبر.");

        RuleFor(x => x.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Bonus.HasValue)
            .WithMessage("يجب أن يكون المكافأة صفرًا أو أكبر.");

        RuleFor(x => x.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Deduction.HasValue)
            .WithMessage("يجب أن يكون الخصم صفرًا أو أكبر.");
    }
}

public sealed class IndividualOutCompanyPayrollEntryRequestValidator : AbstractValidator<IndividualOutCompanyPayrollEntryRequest>
{
    public IndividualOutCompanyPayrollEntryRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("يجب تحديد الموظف.");

        RuleFor(x => x)
            .Must(x => x.StartDate!.Value <= x.EndDate!.Value)
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue && x.StartDate.Value != default && x.EndDate.Value != default);

        RuleFor(x => x.PresentDays)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن يكون عدد أيام الحضور صفرًا أو أكبر.");

        RuleFor(x => x)
            .Must(x => x.PresentDays <= (x.EndDate!.Value.DayNumber - x.StartDate!.Value.DayNumber + 1))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue && x.StartDate.Value != default && x.EndDate.Value != default && x.StartDate.Value <= x.EndDate.Value);

        RuleFor(x => x.WorkedDaysByDayUnit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن تكون وحدات العمل صفرًا أو أكبر.");

        RuleFor(x => x.OvertimeByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.OvertimeByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات العمل الإضافي صفرًا أو أكبر.");

        RuleFor(x => x.DeductionByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DeductionByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات الخصم صفرًا أو أكبر.");

        RuleFor(x => x.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Bonus.HasValue)
            .WithMessage("يجب أن يكون المكافأة صفرًا أو أكبر.");

        RuleFor(x => x.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Deduction.HasValue)
            .WithMessage("يجب أن يكون الخصم صفرًا أو أكبر.");
    }
}

public sealed class BulkOutCompanyPayrollEntryRequestValidator : AbstractValidator<BulkOutCompanyPayrollEntryRequest>
{
    public const int MaximumItemCount = 1000;

    public BulkOutCompanyPayrollEntryRequestValidator()
    {
        RuleFor(x => x.Entries)
            .NotEmpty().WithMessage("يجب إرسال مدخل راتب واحد على الأقل.");

        RuleFor(x => x.Entries)
            .Must(items => items.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن إنشاء قيود رواتب لأكثر من {MaximumItemCount} موظف في طلب واحد.")
            .When(x => x.Entries is not null);

        RuleForEach(x => x.Entries)
            .SetValidator(new IndividualOutCompanyPayrollEntryRequestValidator())
            .When(x => x.Entries is not null);

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e => (e.StartDate ?? req.DefaultStartDate).HasValue && (e.StartDate ?? req.DefaultStartDate)!.Value != default))
            .WithMessage("تاريخ البداية مطلوب لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e => (e.EndDate ?? req.DefaultEndDate).HasValue && (e.EndDate ?? req.DefaultEndDate)!.Value != default))
            .WithMessage("تاريخ النهاية مطلوب لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e =>
            {
                var s = e.StartDate ?? req.DefaultStartDate;
                var end = e.EndDate ?? req.DefaultEndDate;
                return !s.HasValue || !end.HasValue || s.Value <= end.Value;
            }))
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e =>
            {
                var s = e.StartDate ?? req.DefaultStartDate;
                var end = e.EndDate ?? req.DefaultEndDate;
                if (!s.HasValue || !end.HasValue || s.Value == default || end.Value == default || s.Value > end.Value) return true;
                var totalDays = (end.Value.DayNumber - s.Value.DayNumber) + 1;
                return e.PresentDays <= totalDays;
            }))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must(items => items.Select(item => item.EmployeeId).Distinct().Count() == items.Count)
            .When(x => x.Entries is { Count: > 0 })
            .WithMessage("لا يجوز تكرار نفس الموظف داخل الطلب الواحد.");
    }
}

public sealed class OutCompanyPayrollEntryUpdateRequestValidator : AbstractValidator<OutCompanyPayrollEntryUpdateRequest>
{
    public OutCompanyPayrollEntryUpdateRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("تاريخ البداية مطلوب.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("تاريخ النهاية مطلوب.");

        RuleFor(x => x)
            .Must(x => x.StartDate <= x.EndDate)
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.StartDate != default && x.EndDate != default);

        RuleFor(x => x.PresentDays)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن يكون عدد أيام الحضور صفرًا أو أكبر.");

        RuleFor(x => x)
            .Must(x => x.PresentDays <= (x.EndDate.DayNumber - x.StartDate.DayNumber + 1))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة.")
            .When(x => x.StartDate != default && x.EndDate != default && x.StartDate <= x.EndDate);

        RuleFor(x => x.WorkedDaysByDayUnit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن تكون وحدات العمل صفرًا أو أكبر.");

        RuleFor(x => x.OvertimeByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.OvertimeByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات العمل الإضافي صفرًا أو أكبر.");

        RuleFor(x => x.DeductionByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DeductionByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات الخصم صفرًا أو أكبر.");

        RuleFor(x => x.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Bonus.HasValue)
            .WithMessage("يجب أن يكون المكافأة صفرًا أو أكبر.");

        RuleFor(x => x.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Deduction.HasValue)
            .WithMessage("يجب أن يكون الخصم صفرًا أو أكبر.");
    }
}

public sealed class IndividualOutCompanyPayrollEntryUpdateRequestValidator : AbstractValidator<IndividualOutCompanyPayrollEntryUpdateRequest>
{
    public IndividualOutCompanyPayrollEntryUpdateRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("معرف قيد الراتب غير صالح.");

        RuleFor(x => x)
            .Must(x => x.StartDate!.Value <= x.EndDate!.Value)
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue && x.StartDate.Value != default && x.EndDate.Value != default);

        RuleFor(x => x.PresentDays)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن يكون عدد أيام الحضور صفرًا أو أكبر.");

        RuleFor(x => x)
            .Must(x => x.PresentDays <= (x.EndDate!.Value.DayNumber - x.StartDate!.Value.DayNumber + 1))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue && x.StartDate.Value != default && x.EndDate.Value != default && x.StartDate.Value <= x.EndDate.Value);

        RuleFor(x => x.WorkedDaysByDayUnit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("يجب أن تكون وحدات العمل صفرًا أو أكبر.");

        RuleFor(x => x.OvertimeByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.OvertimeByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات العمل الإضافي صفرًا أو أكبر.");

        RuleFor(x => x.DeductionByDayUnit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DeductionByDayUnit.HasValue)
            .WithMessage("يجب أن تكون وحدات الخصم صفرًا أو أكبر.");

        RuleFor(x => x.Bonus)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Bonus.HasValue)
            .WithMessage("يجب أن يكون المكافأة صفرًا أو أكبر.");

        RuleFor(x => x.Deduction)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Deduction.HasValue)
            .WithMessage("يجب أن يكون الخصم صفرًا أو أكبر.");
    }
}

public sealed class BulkOutCompanyPayrollEntryUpdateRequestValidator : AbstractValidator<BulkOutCompanyPayrollEntryUpdateRequest>
{
    public const int MaximumItemCount = 1000;

    public BulkOutCompanyPayrollEntryUpdateRequestValidator()
    {
        RuleFor(x => x.Entries)
            .NotNull()
            .NotEmpty().WithMessage("يجب إرسال مدخل راتب واحد على الأقل للتعديل.");

        RuleFor(x => x.Entries)
            .Must(items => items.Count <= MaximumItemCount)
            .WithMessage($"لا يمكن تعديل قيود رواتب لأكثر من {MaximumItemCount} قيد في طلب واحد.")
            .When(x => x.Entries is not null);

        RuleForEach(x => x.Entries)
            .SetValidator(new IndividualOutCompanyPayrollEntryUpdateRequestValidator())
            .When(x => x.Entries is not null);

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e => (e.StartDate ?? req.DefaultStartDate).HasValue && (e.StartDate ?? req.DefaultStartDate)!.Value != default))
            .WithMessage("تاريخ البداية مطلوب لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e => (e.EndDate ?? req.DefaultEndDate).HasValue && (e.EndDate ?? req.DefaultEndDate)!.Value != default))
            .WithMessage("تاريخ النهاية مطلوب لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e =>
            {
                var s = e.StartDate ?? req.DefaultStartDate;
                var end = e.EndDate ?? req.DefaultEndDate;
                return !s.HasValue || !end.HasValue || s.Value <= end.Value;
            }))
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must((req, entries) => entries.All(e =>
            {
                var s = e.StartDate ?? req.DefaultStartDate;
                var end = e.EndDate ?? req.DefaultEndDate;
                if (!s.HasValue || !end.HasValue || s.Value == default || end.Value == default || s.Value > end.Value) return true;
                var totalDays = (end.Value.DayNumber - s.Value.DayNumber) + 1;
                return e.PresentDays <= totalDays;
            }))
            .WithMessage("عدد أيام الحضور لا يمكن أن يتجاوز إجمالي عدد الأيام في الفترة لكل مدخل.")
            .When(x => x.Entries is { Count: > 0 });

        RuleFor(x => x.Entries)
            .Must(items => items.Select(item => item.Id).Distinct().Count() == items.Count)
            .When(x => x.Entries is { Count: > 0 })
            .WithMessage("لا يجوز تكرار قيد الراتب داخل الطلب الواحد.");
    }
}
