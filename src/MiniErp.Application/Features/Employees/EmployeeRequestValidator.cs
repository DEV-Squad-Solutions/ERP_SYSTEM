using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Employees
{
    public sealed class EmployeeRequestValidator : AbstractValidator<EmployeeCreateRequest>
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

            RuleFor(x => x.WorkPlaceStatus)
                .IsInEnum()
                .WithMessage("حالة مكان العمل غير صالحة. القيم المقبولة: InCompany أو OutCompany.");
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
                .IsInEnum().WithMessage("نوع الموظف غير صالح، القيم المقبولة: Daily أو Monthly.");

            RuleFor(x => x.Salary)
                .GreaterThan(0).WithMessage("يجب أن يكون الراتب أكبر من صفر.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                    .WithMessage("يجب ألا يتجاوز الراتب منزلتين عشريتين.");

            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .Null().When(x => x.Type == EmployeeType.Daily)
                .WithMessage("لا يمكن تحديد عدد أيام العمل المطلوبة لموظف اليومية.");

            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .InclusiveBetween(1, 31)
                .When(x => x.Type == EmployeeType.Monthly && x.RequiredWorkingDaysPerMonth.HasValue)
                .WithMessage("يجب أن يكون عدد أيام العمل المطلوبة لكل شهر بين 1 و 31.");

            RuleFor(x => x.WorkPlaceStatus)
                .IsInEnum()
                .WithMessage("حالة مكان العمل غير صالحة. القيم المقبولة: InCompany أو OutCompany.");

            RuleFor(x => x.PlaceName)
                .MaximumLength(200).WithMessage("يجب ألا يزيد اسم مكان العمل عن 200 حرف.");
        }
    }

    public sealed class EmployeeUpdateRequestValidator : AbstractValidator<EmployeeUpdateRequest>
    {
        public EmployeeUpdateRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().When(x => x.Name != null).WithMessage("اسم الموظف مطلوب.")
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
                .IsInEnum().When(x => x.Type.HasValue)
                .WithMessage("نوع الموظف غير صالح، القيم المقبولة: Daily أو Monthly.");

            RuleFor(x => x.Salary)
                .GreaterThan(0).When(x => x.Salary.HasValue)
                .WithMessage("يجب أن يكون الراتب أكبر من صفر.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .When(x => x.Salary.HasValue)
                    .WithMessage("يجب ألا يتجاوز الراتب منزلتين عشريتين.");

            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .Null().When(x => x.Type == EmployeeType.Daily)
                .WithMessage("لا يمكن تحديد عدد أيام العمل المطلوبة لموظف اليومية.");

            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .InclusiveBetween(1, 31).When(x => x.RequiredWorkingDaysPerMonth.HasValue)
                .WithMessage("يجب أن يكون عدد أيام العمل المطلوبة لكل شهر بين 1 و 31.");

            RuleFor(x => x.WorkPlaceStatus)
                .IsInEnum().When(x => x.WorkPlaceStatus.HasValue)
                .WithMessage("حالة مكان العمل غير صالحة. القيم المقبولة: InCompany أو OutCompany.");

            RuleFor(x => x.PlaceName)
                .MaximumLength(200).WithMessage("يجب ألا يزيد اسم مكان العمل عن 200 حرف.");
        }
    }
}
