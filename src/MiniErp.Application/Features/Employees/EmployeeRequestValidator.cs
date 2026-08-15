using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Employees
{
    public sealed class EmployeeRequestValidator : AbstractValidator<EmployeeRequest>
    {
        public EmployeeRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الموظف مطلوب.")
                .MaximumLength(200).WithMessage("يجب ألا يزيد اسم الموظف عن 200 حرف.");

            RuleFor(x => x.JobTitle)
                .MaximumLength(200).WithMessage("يجب ألا يزيد المسمى الوظيفي عن 200 حرف.");

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(50).WithMessage("يجب ألا يزيد رقم الهاتف عن 50 حرفًا.");

            RuleFor(x => x.Email)
                .MaximumLength(256).WithMessage("يجب ألا يزيد البريد الإلكتروني عن 256 حرفًا.")
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                    .WithMessage("صيغة البريد الإلكتروني غير صحيحة.");

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("يجب ألا يزيد العنوان عن 500 حرف.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("نوع الموظف غير صالح، القيم المقبولة: 0 (يومي) أو 1 (شهري).");

            RuleFor(x => x.Salary)
                .NotNull().WithMessage("الراتب مطلوب.")
                .GreaterThan(0).WithMessage("يجب أن يكون الراتب أكبر من صفر.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                    .WithMessage("يجب ألا يتجاوز الراتب منزلتين عشريتين.");

            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .GreaterThan(0).WithMessage("يجب أن يكون عدد أيام العمل المطلوبة أكبر من صفر.")
                .When(x => x.RequiredWorkingDaysPerMonth.HasValue);
        }
    }

    public sealed class EmployeeCreateRequestValidator : AbstractValidator<EmployeeCreateRequest>
    {
        public EmployeeCreateRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الموظف مطلوب.")
                .MaximumLength(200).WithMessage("يجب ألا يزيد اسم الموظف عن 200 حرف.");

            RuleFor(x => x.JobTitle)
                .MaximumLength(200).WithMessage("يجب ألا يزيد المسمى الوظيفي عن 200 حرف.");

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(50).WithMessage("يجب ألا يزيد رقم الهاتف عن 50 حرفًا.");

            RuleFor(x => x.Email)
                .MaximumLength(256).WithMessage("يجب ألا يزيد البريد الإلكتروني عن 256 حرفًا.")
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                    .WithMessage("صيغة البريد الإلكتروني غير صحيحة.");

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("يجب ألا يزيد العنوان عن 500 حرف.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("نوع الموظف غير صالح، القيم المقبولة: 0 (يومي) أو 1 (شهري).");

            RuleFor(x => x.Salary)
                .NotNull().WithMessage("الراتب مطلوب.")
                .GreaterThan(0).WithMessage("يجب أن يكون الراتب أكبر من صفر.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                    .WithMessage("يجب ألا يتجاوز الراتب منزلتين عشريتين.");

            // RequiredWorkingDaysPerMonth is required and validated only for Monthly employees
            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .NotNull().WithMessage("عدد أيام العمل المطلوبة شهريًا مطلوب للموظف الشهري.")
                .GreaterThan(0).WithMessage("يجب أن يكون عدد أيام العمل المطلوبة أكبر من صفر.")
                .When(x => x.Type == EmployeeType.Monthly);
        }
    }
}
