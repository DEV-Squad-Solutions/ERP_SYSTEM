using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Accounts;

public static class AccountErrors
{
    public static Error InvalidId() => Error.Validation(
        "Accounts.InvalidId",
        "يجب أن يكون رقم الحساب أكبر من صفر.");

    public static Error NotFound(int id) => Error.NotFound(
        "Accounts.NotFound",
        $"لم يتم العثور على الحساب رقم {id}.");

    public static Error CodeExists(string code) => Error.Conflict(
        "Accounts.CodeExists",
        $"كود الحساب '{code}' مستخدم بالفعل في نفس الشركة.",
        nameof(AccountRequest.Code));

    public static Error RowVersionRequired() => Error.Validation(
        "Accounts.RowVersionRequired",
        "يجب إرسال إصدار الحساب الحالي للتعديل.",
        nameof(AccountUpdateRequest.RowVersion));

    public static Error Concurrency() => Error.Conflict(
        "Accounts.Concurrency",
        "تم تعديل الحساب بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error ParentNotFound(int parentId) => Error.Validation(
        "Accounts.ParentNotFound",
        $"الحساب الأب رقم {parentId} غير موجود في الشركة الحالية.",
        nameof(AccountRequest.ParentAccountId));

    public static Error ParentInactive() => Error.Validation(
        "Accounts.ParentInactive",
        "يجب أن يكون الحساب الأب فعالًا.",
        nameof(AccountRequest.ParentAccountId));

    public static Error ParentMustBeGroup() => Error.Validation(
        "Accounts.ParentMustBeGroup",
        "لا يمكن إضافة حساب فرعي تحت حساب يسمح بالتسجيل.",
        nameof(AccountRequest.ParentAccountId));

    public static Error ParentCannotBeSelf() => Error.Validation(
        "Accounts.ParentCannotBeSelf",
        "لا يمكن اختيار الحساب نفسه كحساب أب.",
        nameof(AccountRequest.ParentAccountId));

    public static Error HierarchyCycle() => Error.Conflict(
        "Accounts.HierarchyCycle",
        "لا يمكن اختيار هذا الحساب الأب لأنه سيكوّن دورة في شجرة الحسابات.",
        nameof(AccountRequest.ParentAccountId));

    public static Error PostingAccountHasChildren() => Error.Conflict(
        "Accounts.PostingAccountHasChildren",
        "لا يمكن تحويل الحساب إلى حساب قابل للتسجيل لأنه يحتوي على حسابات فرعية.",
        nameof(AccountRequest.IsPosting));

    public static Error InactiveAccountHasChildren() => Error.Conflict(
        "Accounts.InactiveAccountHasChildren",
        "لا يمكن إلغاء تنشيط حساب يحتوي على حسابات فرعية فعالة.",
        nameof(AccountRequest.IsActive));

    public static Error MappedAccountCannotChangeClassification() => Error.Conflict(
        "Accounts.MappedAccountCannotChangeClassification",
        "لا يمكن تغيير نوع الحساب أو جعله حسابًا رئيسيًا لوجود ربط قائم بالقوائم المالية.");

    public static Error AccountWithChildrenCannotChangeClassification() => Error.Conflict(
        "Accounts.AccountWithChildrenCannotChangeClassification",
        "لا يمكن تغيير نوع أو طبيعة حساب رئيسي يحتوي على حسابات فرعية.");

    public static Error HasChildren() => Error.Conflict(
        "Accounts.HasChildren",
        "لا يمكن حذف الحساب لوجود حسابات فرعية مرتبطة به.");

    public static Error HasStatementMappings() => Error.Conflict(
        "Accounts.HasStatementMappings",
        "لا يمكن حذف الحساب لوجود ربط حالي أو تاريخي بالقوائم المالية. يمكن إلغاء تنشيطه بدلًا من ذلك.");

    public static Error HasCashVouchers() => Error.Conflict(
        "Accounts.HasCashVouchers",
        "لا يمكن حذف الحساب لوجود سندات قبض أو صرف مرتبطة به. يمكن إلغاء تنشيطه بدلًا من ذلك.");

    public static Error HasJournalEntries() => Error.Conflict(
        "Accounts.HasJournalEntries",
        "لا يمكن حذف الحساب لوجود قيود يومية مرتبطة به. يمكن إلغاء تنشيطه بدلًا من ذلك.");
}
