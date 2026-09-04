using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeMovements;

public static class EmployeeMovementErrors
{
    public static Error NotFound(int id) =>
        Error.NotFound(
            "EmployeeMovements.NotFound",
            $"حركة الموظف برقم '{id}' غير موجودة.");

    public static Error InvalidId() =>
        Error.Validation(
            "EmployeeMovements.InvalidId",
            "معرف حركة الموظف غير صالح.");

    public static Error EmployeeNotFound(int employeeId) =>
        Error.NotFound(
            "EmployeeMovements.EmployeeNotFound",
            $"الموظف برقم '{employeeId}' غير موجود.");

    public static Error EmployeeInactive(int employeeId) =>
        Error.Validation(
            "EmployeeMovements.EmployeeInactive",
            $"الموظف برقم '{employeeId}' غير نشط.");

    public static Error CashboxRequired() =>
        Error.Validation(
            "EmployeeMovements.CashboxRequired",
            "لا يمكن إنشاء حركة موظف بدون خزينة.");

    public static Error CashboxRequiredForAdvance() => CashboxRequired();

    public static Error CashboxNotFound(int cashboxId) =>
        Error.NotFound(
            "EmployeeMovements.CashboxNotFound",
            $"الخزينة المحددة برقم '{cashboxId}' غير موجودة.");

    public static Error CashboxInactive(int cashboxId) =>
        Error.Validation(
            "EmployeeMovements.CashboxInactive",
            $"الخزينة المحددة برقم '{cashboxId}' غير نشطة.");

    public static Error ExchangeRateRequired() =>
        Error.Validation(
            "EmployeeMovements.ExchangeRateRequired",
            "سعر الصرف مطلوب ويجب أن يكون أكبر من صفر للعملات الأجنبية.");

    public static Error CashboxMustBeEgp() =>
        Error.Validation(
            "EmployeeMovements.CashboxMustBeEgp",
            "يجب أن تكون الخزينة المحددة لحركات الموظف بالجنيه المصري (EGP).");

    public static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "CashVouchers.InsufficientBalance",
            $"Cashbox {cashboxId} does not have enough balance.");

    public static Error InvalidAmount() =>
        Error.Validation(
            "EmployeeMovements.InvalidAmount",
            "يجب أن يكون المبلغ أكبر من صفر.");
}
