using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashVouchers;

public static class CashVoucherErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "CashVouchers.InvalidId",
            "يجب أن يكون رقم سند النقدية أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "CashVouchers.NotFound",
            $"لم يتم العثور على سند النقدية رقم {id}.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "CashVouchers.RowVersionRequired",
            "يجب إرسال إصدار سند النقدية الحالي للتعديل.",
            nameof(CashVoucherUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "CashVouchers.Concurrency",
            "تم تعديل سند النقدية بواسطة مستخدم آخر. أعد تحميل السند ثم حاول مرة أخرى.");

    public static Error InvoiceGeneratedReadOnly() =>
        Error.Conflict(
            "CashVouchers.InvoiceGeneratedReadOnly",
            "لا يمكن تعديل أو حذف سند السداد المنشأ تلقائياً من الفاتورة؛ عدّل الفاتورة نفسها.");

    public static Error CashboxNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.CashboxNotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.",
            nameof(CashVoucherRequest.CashboxId));

    public static Error CashboxInactive() =>
        Error.Conflict(
            "CashVouchers.CashboxInactive",
            "لا يمكن استخدام صندوق نقدية غير نشط في سند جديد.",
            nameof(CashVoucherRequest.CashboxId));

    public static Error MovementTypeNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.MovementTypeNotFound",
            $"لم يتم العثور على نوع الحركة النقدية رقم {id}.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    public static Error MovementTypeInactive() =>
        Error.Conflict(
            "CashVouchers.MovementTypeInactive",
            "لا يمكن استخدام نوع حركة نقدية غير نشط في سند جديد.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    public static Error MovementTypeDirectionMismatch() =>
        Error.Conflict(
            "CashVouchers.MovementTypeDirectionMismatch",
            "اتجاه نوع الحركة النقدية لا يطابق اتجاه السند.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    public static Error MovementTypeNotForPartner() =>
        Error.Conflict(
            "CashVouchers.MovementTypeNotForPartner",
            "نوع الحركة النقدية المختار غير مخصص لحسابات العملاء أو الموردين.",
            nameof(CashVoucherRequest.CashMovementTypeId));

    public static Error MovementTypeForPartnerOnly() =>
        Error.Conflict(
            "CashVouchers.MovementTypeForPartnerOnly",
            "نوع الحركة النقدية المختار مخصص للعملاء أو الموردين فقط.",
            nameof(CashVoucherRequest.PartyType));

    public static Error PartnerNotFound(int? id) =>
        Error.NotFound(
            "CashVouchers.PartnerNotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.",
            nameof(CashVoucherRequest.BusinessPartnerId));

    public static Error PartnerCurrencyMismatch() =>
        Error.Conflict(
            "CashVouchers.PartnerCurrencyMismatch",
            "عملة صندوق النقدية لا تطابق عملة العميل أو المورد.",
            nameof(CashVoucherRequest.BusinessPartnerId));

    public static Error DriverNotFound(int? id) =>
        Error.NotFound(
            "CashVouchers.DriverNotFound",
            $"لم يتم العثور على السائق رقم {id}.",
            nameof(CashVoucherRequest.DriverId));

    public static Error DriverTripNotFound(int id) =>
        Error.NotFound(
            "CashVouchers.DriverTripNotFound",
            $"لم يتم العثور على رحلة رقم {id} تخص السائق المحدد.",
            nameof(CashVoucherRequest.DriverTripId));

    public static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "CashVouchers.InsufficientCashboxBalance",
            $"الرصيد المتاح في صندوق النقدية رقم {cashboxId} لا يسمح بهذه العملية.");
}
