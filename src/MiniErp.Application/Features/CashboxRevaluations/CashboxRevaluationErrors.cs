using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashboxRevaluations;

public static class CashboxRevaluationErrors
{
    public static Error InvalidRequest() => Error.Validation(
        "CashboxRevaluations.InvalidRequest", "بيانات إعادة تقييم الخزينة غير صحيحة.");

    public static Error CashboxNotFound(int id) => Error.NotFound(
        "CashboxRevaluations.CashboxNotFound", $"الخزينة رقم {id} غير موجودة.");

    public static Error BaseCurrencyCashbox() => Error.Validation(
        "CashboxRevaluations.BaseCurrencyCashbox", "لا يمكن إعادة تقييم خزينة بعملة الشركة الأساسية.");

    public static Error RateInvalid() => Error.Validation(
        "CashboxRevaluations.RateInvalid", "سعر الإقفال يجب أن يكون أكبر من صفر.");

    public static Error FiscalYearClosed() => Error.Conflict(
        "CashboxRevaluations.FiscalYearClosed", "السنة المالية لتاريخ إعادة التقييم مغلقة أو غير موجودة.");

    public static Error Duplicate(int cashboxId, DateOnly date) => Error.Conflict(
        "CashboxRevaluations.Duplicate", $"تمت إعادة تقييم الخزينة {cashboxId} بتاريخ {date:yyyy-MM-dd} من قبل.");

    public static Error Backdated(DateOnly date) => Error.Conflict(
        "CashboxRevaluations.Backdated", $"لا يمكن إنشاء إعادة تقييم بتاريخ {date:yyyy-MM-dd} قبل إعادة تقييم لاحقة.");

    public static Error BeforeOpeningBalance(DateOnly date) => Error.Validation(
        "CashboxRevaluations.BeforeOpeningBalance",
        $"تاريخ إعادة التقييم {date:yyyy-MM-dd} يسبق تاريخ بداية رصيد الخزينة.");

    public static Error NegativeBalance() => Error.Validation(
        "CashboxRevaluations.NegativeBalance",
        "لا يمكن إعادة تقييم خزينة رصيد عملتها أو قيمتها الدفترية سالب؛ صحح الحركات أولاً.");

    public static Error AccountMappingMissing() => Error.Validation(
        "CashboxRevaluations.AccountMappingMissing", "يجب إعداد حساب الخزينة وحساب فروق العملة للسنة المالية.");

    public static Error PostingBlocked(DateOnly date) => Error.Conflict(
        "CashboxRevaluations.PostingBlocked",
        $"لا يمكن تعديل أو حذف حركة خزينة بتاريخ {date:yyyy-MM-dd} أو قبله بعد تسجيل إعادة تقييم لاحقة.");
}
