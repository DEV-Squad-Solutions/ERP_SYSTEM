using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashboxTransfers;

public static class CashboxTransferErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "CashboxTransfers.InvalidId",
            "رقم تحويل الخزائن غير صحيح.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "CashboxTransfers.NotFound",
            $"لم يتم العثور على تحويل الخزائن رقم {id}.");

    public static Error InvalidRequest() =>
        Error.Validation(
            "CashboxTransfers.InvalidRequest",
            "بيانات تحويل الخزائن غير صحيحة.");

    public static Error CashboxesMustDiffer() =>
        Error.Validation(
            "CashboxTransfers.CashboxesMustDiffer",
            "يجب اختيار خزنة مستلمة مختلفة عن الخزنة المصدر.",
            nameof(CashboxTransferRequest.DestinationCashboxId));

    public static Error CashboxNotFound(int id, string fieldName) =>
        Error.NotFound(
            "CashboxTransfers.CashboxNotFound",
            $"لم يتم العثور على الخزنة رقم {id}.",
            fieldName);

    public static Error CashboxInactive(int id, string fieldName) =>
        Error.Conflict(
            "CashboxTransfers.CashboxInactive",
            $"الخزنة رقم {id} غير نشطة.",
            fieldName);

    public static Error CurrencyMismatch() =>
        Error.Conflict(
            "CashboxTransfers.CurrencyMismatch",
            "التحويل بين خزنتين بعملتين مختلفتين غير مدعوم حاليًا.",
            nameof(CashboxTransferRequest.DestinationCashboxId));

    public static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "CashboxTransfers.InsufficientCashboxBalance",
            $"الرصيد المتاح في الخزنة رقم {cashboxId} لا يسمح بالتحويل.",
            nameof(CashboxTransferRequest.Amount));

    public static Error RowVersionRequired() =>
        Error.Validation(
            "CashboxTransfers.RowVersionRequired",
            "أعد تحميل التحويل ثم حاول التعديل مرة أخرى.",
            nameof(CashboxTransferUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "CashboxTransfers.Concurrency",
            "تم تعديل تحويل الخزائن بواسطة مستخدم آخر. أعد تحميله ثم حاول مرة أخرى.");

    public static Error InvalidVoucherPair() =>
        Error.Conflict(
            "CashboxTransfers.InvalidVoucherPair",
            "سندا الصرف والقبض المرتبطان بالتحويل غير مكتملين.");

    public static Error FiltersInvalid() =>
        Error.Validation(
            "CashboxTransfers.FiltersInvalid",
            "مرشحات تحويلات الخزائن غير صحيحة.");
}
