using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.MonetaryAccountRevaluations;

public static class MonetaryAccountRevaluationErrors
{
    public static Error InvalidRequest() => Error.Validation(
        "MonetaryAccountRevaluations.InvalidRequest", "بيانات إعادة تقييم الحساب غير صحيحة.");

    public static Error AccountNotFound(int id) => Error.NotFound(
        "MonetaryAccountRevaluations.AccountNotFound", $"الحساب رقم {id} غير موجود أو غير مرحل.");

    public static Error PartyRequired() => Error.Validation(
        "MonetaryAccountRevaluations.PartyRequired", "يجب إرسال نوع الطرف ورقم الطرف معًا أو تركهما معًا.");

    public static Error PartyNotFound() => Error.NotFound(
        "MonetaryAccountRevaluations.PartyNotFound", "الطرف المرتبط بالحساب غير موجود أو غير نشط.");

    public static Error PartyCurrencyMismatch() => Error.Validation(
        "MonetaryAccountRevaluations.PartyCurrencyMismatch", "عملة الطرف لا تطابق العملة المطلوبة لإعادة التقييم.");

    public static Error UnsupportedParty() => Error.Validation(
        "MonetaryAccountRevaluations.UnsupportedParty", "إعادة التقييم مدعومة حاليًا للخزائن والعملاء والموردين فقط؛ الموظفون والسائقون لا يملكون عملة أجنبية مستقلة.");

    public static Error BaseCurrencyAccount() => Error.Validation(
        "MonetaryAccountRevaluations.BaseCurrencyAccount", "لا يمكن إعادة تقييم حساب بعملة الشركة الأساسية.");

    public static Error RateInvalid() => Error.Validation(
        "MonetaryAccountRevaluations.RateInvalid", "سعر الإقفال يجب أن يكون أكبر من صفر.");

    public static Error FiscalYearClosed() => Error.Conflict(
        "MonetaryAccountRevaluations.FiscalYearClosed", "السنة المالية لتاريخ إعادة التقييم مغلقة أو غير موجودة.");

    public static Error Duplicate(DateOnly date) => Error.Conflict(
        "MonetaryAccountRevaluations.Duplicate", $"تمت إعادة تقييم نفس الحساب/الطرف بتاريخ {date:yyyy-MM-dd} من قبل.");

    public static Error Backdated(DateOnly date) => Error.Conflict(
        "MonetaryAccountRevaluations.Backdated", $"لا يمكن إنشاء إعادة تقييم بتاريخ {date:yyyy-MM-dd} قبل إعادة تقييم لاحقة.");

    public static Error NegativeBalance() => Error.Validation(
        "MonetaryAccountRevaluations.NegativeBalance", "لا يمكن إعادة تقييم رصيد عملة أو قيمة دفترية سالبة.");

    public static Error AccountMappingMissing() => Error.Validation(
        "MonetaryAccountRevaluations.AccountMappingMissing", "يجب إعداد الحساب المرتبط بالطرف وحساب فروق العملة للسنة المالية.");
}
