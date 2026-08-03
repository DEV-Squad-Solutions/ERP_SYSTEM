using MiniErp.Domain.Enums;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Services.Employees
{
    public sealed partial class EmployeeService
    {
        private static Error InvalidId() =>
        Error.Validation(
            "Invoices.InvalidId",
            "يجب أن يكون رقم أكبر من صفر ID .");

        public static Error CodeTooLong() =>
            Error.Validation(
                "Employee.CodeTooLong", 
                "يجب ألا يزيد كود الموظف عن 50 حرفًا.");

        public static Error PhoneNumberTooLong() =>
            Error.Validation(
                "Employee.PhoneNumberTooLong", 
                "يجب ألا يزيد رقم الهاتف عن 50 حرفًا.");

        public static  Error InvalidEmail() =>
            Error.Validation(
                "Employee.InvalidEmail", 
                "البريد الإلكتروني غير صالح.");

        public static  Error InvalidEmployeeType() =>
            Error.Validation(
                "Employee.InvalidType", 
                "نوع الموظف غير صحيح.");

        public static Error InvalidDailyRate() =>
            Error.Validation(
                "Employee.InvalidDailyRate", 
                "الأجر اليومي غير صالح.");

        public static Error InvalidMonthlySalary() =>
            Error.Validation(
                "Employee.InvalidMonthlySalary", 
                "الراتب الشهري غير صالح.");
    }
}
