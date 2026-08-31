using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FinancialStatementLines;

public static class FinancialStatementLineErrors
{
    public static Error InvalidId() => Error.Validation(
        "FinancialStatementLines.InvalidId",
        "يجب أن يكون رقم بند القائمة المالية أكبر من صفر.");

    public static Error NotFound(int id) => Error.NotFound(
        "FinancialStatementLines.NotFound",
        $"لم يتم العثور على بند القائمة المالية رقم {id}.");

    public static Error FiscalYearNotFound(int id) => Error.Validation(
        "FinancialStatementLines.FiscalYearNotFound",
        $"السنة المالية رقم {id} غير موجودة في الشركة الحالية.",
        nameof(FinancialStatementLineRequest.FiscalYearId));

    public static Error FiscalYearClosed() => Error.Conflict(
        "FinancialStatementLines.FiscalYearClosed",
        "لا يمكن تغيير تشكيل القوائم لسنة مالية مغلقة. أعد فتح السنة أولًا.",
        nameof(FinancialStatementLineRequest.FiscalYearId));

    public static Error CodeExists(string code) => Error.Conflict(
        "FinancialStatementLines.CodeExists",
        $"كود بند القائمة '{code}' مستخدم بالفعل في نفس السنة ونوع القائمة.",
        nameof(FinancialStatementLineRequest.Code));

    public static Error RowVersionRequired() => Error.Validation(
        "FinancialStatementLines.RowVersionRequired",
        "يجب إرسال إصدار بند القائمة الحالي للتعديل.",
        nameof(FinancialStatementLineUpdateRequest.RowVersion));

    public static Error Concurrency() => Error.Conflict(
        "FinancialStatementLines.Concurrency",
        "تم تعديل بند القائمة بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error ParentNotFound(int parentId) => Error.Validation(
        "FinancialStatementLines.ParentNotFound",
        $"بند القائمة الأب رقم {parentId} غير موجود في نفس السنة ونوع القائمة.",
        nameof(FinancialStatementLineRequest.ParentLineId));

    public static Error ParentCannotBeSelf() => Error.Validation(
        "FinancialStatementLines.ParentCannotBeSelf",
        "لا يمكن اختيار بند القائمة نفسه كبند أب.",
        nameof(FinancialStatementLineRequest.ParentLineId));

    public static Error ParentInactive() => Error.Validation(
        "FinancialStatementLines.ParentInactive",
        "يجب أن يكون بند القائمة الأب فعالًا.",
        nameof(FinancialStatementLineRequest.ParentLineId));

    public static Error ParentMustBeGroup() => Error.Validation(
        "FinancialStatementLines.ParentMustBeGroup",
        "لا يمكن إضافة بند فرعي تحت بند يسمح بربط الحسابات.",
        nameof(FinancialStatementLineRequest.ParentLineId));

    public static Error HierarchyCycle() => Error.Conflict(
        "FinancialStatementLines.HierarchyCycle",
        "لا يمكن اختيار هذا البند الأب لأنه سيكوّن دورة في شجرة القائمة المالية.",
        nameof(FinancialStatementLineRequest.ParentLineId));

    public static Error HasChildren() => Error.Conflict(
        "FinancialStatementLines.HasChildren",
        "لا يمكن حذف بند القائمة لوجود بنود فرعية مرتبطة به.");

    public static Error HasMappings() => Error.Conflict(
        "FinancialStatementLines.HasMappings",
        "لا يمكن حذف بند القائمة لوجود حسابات مرتبطة به.");

    public static Error AssignableLineHasChildren() => Error.Conflict(
        "FinancialStatementLines.AssignableLineHasChildren",
        "لا يمكن جعل البند قابلًا لربط الحسابات لأنه يحتوي على بنود فرعية.",
        nameof(FinancialStatementLineRequest.IsAssignable));

    public static Error InactiveLineHasChildren() => Error.Conflict(
        "FinancialStatementLines.InactiveLineHasChildren",
        "لا يمكن إلغاء تنشيط بند يحتوي على بنود فرعية فعالة.",
        nameof(FinancialStatementLineRequest.IsActive));

    public static Error MappedLineCannotBeDisabled() => Error.Conflict(
        "FinancialStatementLines.MappedLineCannotBeDisabled",
        "لا يمكن إلغاء تنشيط البند أو منعه من الربط لوجود حسابات مرتبطة به.");

    public static Error MappedLineCannotChangeScope() => Error.Conflict(
        "FinancialStatementLines.MappedLineCannotChangeScope",
        "لا يمكن تغيير السنة أو نوع القائمة لبند مرتبط بحسابات.");

    public static Error ParentScopeCannotChange() => Error.Conflict(
        "FinancialStatementLines.ParentScopeCannotChange",
        "لا يمكن تغيير السنة أو نوع القائمة لبند يحتوي على بنود فرعية.");

    public static Error InactiveOrNotAssignable(int id) => Error.Validation(
        "FinancialStatementLines.InactiveOrNotAssignable",
        $"بند القائمة رقم {id} غير فعال أو لا يسمح بربط الحسابات.");

    public static Error InvalidStatementType(FinancialStatementType statementType) =>
        Error.Validation(
            "FinancialStatementLines.InvalidStatementType",
            $"نوع القائمة المالية '{statementType}' غير مدعوم.",
            nameof(FinancialStatementLineRequest.StatementType));
}
