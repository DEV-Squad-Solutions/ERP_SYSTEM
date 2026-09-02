using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountStatementMappings;

public static class AccountStatementMappingErrors
{
    public static Error InvalidStatementType(FinancialStatementType statementType) =>
        Error.Validation(
            "AccountStatementMappings.InvalidStatementType",
            $"نوع القائمة المالية '{statementType}' غير مدعوم.",
            "StatementType");

    public static Error FiscalYearNotFound(int id) => Error.NotFound(
        "AccountStatementMappings.FiscalYearNotFound",
        $"السنة المالية رقم {id} غير موجودة في الشركة الحالية.");

    public static Error FiscalYearClosed() => Error.Conflict(
        "AccountStatementMappings.FiscalYearClosed",
        "لا يمكن تغيير ربط القوائم لسنة مالية مغلقة. أعد فتح السنة أولًا.");

    public static Error DuplicateAccount(int accountId, int index) =>
        Error.Validation(
            "AccountStatementMappings.DuplicateAccount",
            $"الحساب رقم {accountId} مكرر داخل نفس نوع القائمة والسنة المالية.",
            $"Mappings[{index}].AccountId");

    public static Error AccountNotFound(int accountId, int index) =>
        Error.Validation(
            "AccountStatementMappings.AccountNotFound",
            $"الحساب رقم {accountId} غير موجود في الشركة الحالية.",
            $"Mappings[{index}].AccountId");

    public static Error AccountNotPostingOrInactive(int accountId, int index) =>
        Error.Validation(
            "AccountStatementMappings.AccountNotPostingOrInactive",
            $"الحساب رقم {accountId} غير فعال أو لا يسمح بالتسجيل.",
            $"Mappings[{index}].AccountId");

    public static Error IncompatibleAccountType(int accountId, int index) =>
        Error.Validation(
            "AccountStatementMappings.IncompatibleAccountType",
            $"نوع الحساب رقم {accountId} غير متوافق مع نوع القائمة المالية.",
            $"Mappings[{index}].AccountId");

    public static Error LineNotFound(int lineId, int index) =>
        Error.Validation(
            "AccountStatementMappings.LineNotFound",
            $"بند القائمة رقم {lineId} غير موجود في نفس السنة ونوع القائمة.",
            $"Mappings[{index}].FinancialStatementLineId");

    public static Error LineNotAssignable(int lineId, int index) =>
        Error.Validation(
            "AccountStatementMappings.LineNotAssignable",
            $"بند القائمة رقم {lineId} غير فعال أو لا يسمح بربط الحسابات.",
            $"Mappings[{index}].FinancialStatementLineId");

    public static Error ReplaceConflict() => Error.Conflict(
        "AccountStatementMappings.ReplaceConflict",
        "تعذر حفظ ربط القوائم بسبب تعديل متزامن. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
