using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountMappings;

public static class AccountMappingErrors
{
    public static Error FiscalYearNotFound(int id) => Error.NotFound(
        "AccountMappings.FiscalYearNotFound",
        $"السنة المالية رقم {id} غير موجودة في الشركة الحالية.");

    public static Error FiscalYearClosed() => Error.Conflict(
        "AccountMappings.FiscalYearClosed",
        "لا يمكن تعديل الربط المحاسبي لسنة مالية مغلقة. أعد فتح السنة أولًا.");

    public static Error InvalidMappingType(
        AccountingMappingType mappingType,
        int index) => Error.Validation(
        "AccountMappings.InvalidMappingType",
        $"نوع الربط المحاسبي '{mappingType}' غير مدعوم.",
        $"Mappings[{index}].MappingType");

    public static Error SourceRequired(int index) => Error.Validation(
        "AccountMappings.SourceRequired",
        "يجب تحديد المصدر لهذا النوع من الربط.",
        $"Mappings[{index}].SourceId");

    public static Error SourceNotAllowed(int index) => Error.Validation(
        "AccountMappings.SourceNotAllowed",
        "هذا النوع من الربط لا يقبل مصدرًا محددًا.",
        $"Mappings[{index}].SourceId");

    public static Error DuplicateMapping(int index) => Error.Validation(
        "AccountMappings.DuplicateMapping",
        "نوع الربط والمصدر مكرران داخل نفس السنة المالية.",
        $"Mappings[{index}]");

    public static Error SourceNotFound(
        AccountingMappingType mappingType,
        int sourceId,
        int index) => Error.Validation(
        "AccountMappings.SourceNotFound",
        $"المصدر رقم {sourceId} الخاص بنوع الربط '{mappingType}' غير موجود في الشركة الحالية.",
        $"Mappings[{index}].SourceId");

    public static Error AccountNotFound(int accountId, int index) => Error.Validation(
        "AccountMappings.AccountNotFound",
        $"الحساب رقم {accountId} غير موجود في الشركة الحالية.",
        $"Mappings[{index}].AccountId");

    public static Error AccountNotPostingOrInactive(int accountId, int index) =>
        Error.Validation(
            "AccountMappings.AccountNotPostingOrInactive",
            $"الحساب رقم {accountId} غير فعال أو لا يسمح بالتسجيل.",
            $"Mappings[{index}].AccountId");

    public static Error IncompatibleAccountType(
        AccountingMappingType mappingType,
        int accountId,
        int index) => Error.Validation(
        "AccountMappings.IncompatibleAccountType",
        $"الحساب رقم {accountId} غير متوافق مع نوع الربط '{mappingType}'.",
        $"Mappings[{index}].AccountId");

    public static Error ReplaceConflict() => Error.Conflict(
        "AccountMappings.ReplaceConflict",
        "تعذر حفظ الربط المحاسبي بسبب تعديل متزامن. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
