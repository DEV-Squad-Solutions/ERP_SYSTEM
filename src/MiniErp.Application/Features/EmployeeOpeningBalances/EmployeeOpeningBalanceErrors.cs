using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public static class EmployeeOpeningBalanceErrors
{
    public static Error RowVersionRequired() =>
        Error.Validation(
            "EmployeeOpeningBalances.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي للتعديل.",
            nameof(EmployeeOpeningBalanceUpdateRequest.RowVersion));

    public static Error EmployeeNotFound(int id) =>
        Error.NotFound(
            "EmployeeOpeningBalances.EmployeeNotFound",
            $"لم يتم العثور على الموظف رقم {id}.",
            nameof(EmployeeOpeningBalanceRequest.EmployeeId));

    public static Error EmployeeInactive() =>
        Error.Conflict(
            "EmployeeOpeningBalances.EmployeeInactive",
            "لا يمكن استخدام موظف غير نشط.",
            nameof(EmployeeOpeningBalanceRequest.EmployeeId));

    public static Error CurrencyMismatch() =>
        Error.Conflict(
            "EmployeeOpeningBalances.CurrencyMismatch",
            "يجب أن تطابق عملة رصيد الموظف عملة الموظف المحددة.",
            nameof(EmployeeOpeningBalanceRequest.Currency));

    public static Error InvalidId() =>
        Error.Validation(
            "EmployeeOpeningBalances.InvalidId",
            "يجب أن يكون رقم رصيد الموظف أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "EmployeeOpeningBalances.NotFound",
            $"لم يتم العثور على رصيد الموظف رقم {id}.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "EmployeeOpeningBalances.DocumentNumberExists",
            $"رقم المستند '{number}' مستخدم بالفعل.",
            "DocumentNumber");

    public static Error Concurrency() =>
        Error.Conflict(
            "EmployeeOpeningBalances.Concurrency",
            "تم تعديل رصيد الموظف بواسطة عملية أخرى. أعد تحميل المستند ثم حاول مرة أخرى.");

    public static Error CannotModifyPayrollGeneratedBalance() =>
        Error.Conflict(
            "EmployeeOpeningBalances.CannotModifyPayrollGenerated",
            "لا يمكن تعديل رصيد تم إنشاؤه تلقائيًا من مسير الرواتب.");

    public static Error CannotDeletePayrollGeneratedBalance() =>
        Error.Conflict(
            "EmployeeOpeningBalances.CannotDeletePayrollGenerated",
            "لا يمكن حذف رصيد تم إنشاؤه تلقائيًا من مسير الرواتب.");
}
