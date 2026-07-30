using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private static Error InvalidId() =>
        Error.Validation(
            "Invoices.InvalidId",
            "يجب أن يكون رقم الفاتورة أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Invoices.NotFound",
            $"لم يتم العثور على الفاتورة رقم {id}.");

    private static Error Concurrency() =>
        Error.Conflict(
            "Invoices.Concurrency",
            "تم تعديل الفاتورة بواسطة مستخدم آخر. أعد تحميل الفاتورة ثم حاول مرة أخرى.");

    private static Error DriverTripHasCashVouchers() =>
        Error.Conflict(
            "Invoices.DriverTripHasCashVouchers",
            "لا يمكن تعديل الفاتورة أو حذفها لأن رحلة السائق المرتبطة بها مستخدمة في سندات نقدية حالية أو تاريخية.");

    private static Error LinkedSalesReturnsExist() =>
        Error.Conflict(
            "Invoices.LinkedSalesReturnsExist",
            "لا يمكن حذف فاتورة البيع لأن هناك مرتجع بيع نشطاً مرتبطاً بأحد سطورها.");

    private static Error CashInvoiceMustBeFullyPaid() =>
        Error.Validation(
            "Invoices.CashInvoiceMustBeFullyPaid",
            "الفاتورة النقدية يجب أن تكون مدفوعة بالكامل.");

    private static Error CreditInvoiceCannotBeFullyPaid() =>
        Error.Validation(
            "Invoices.CreditInvoiceCannotBeFullyPaid",
            "الفاتورة الآجلة لا تقبل السداد الكامل؛ استخدم الفاتورة النقدية.");

    private static Error CashboxRequiredForPayment() =>
        Error.Validation(
            "Invoices.CashboxRequiredForPayment",
            "صندوق النقدية مطلوب عند تسجيل دفعة.",
            nameof(InvoiceRequest.CashboxId));

    private static Error CashMovementTypeRequiredForPayment() =>
        Error.Validation(
            "Invoices.CashMovementTypeRequiredForPayment",
            "نوع الحركة النقدية مطلوب عند تسجيل دفعة.",
            nameof(InvoiceRequest.CashMovementTypeId));

    private static Error PaymentReferencesNotAllowed() =>
        Error.Validation(
            "Invoices.PaymentReferencesNotAllowed",
            "لا يجوز تحديد صندوق أو نوع حركة نقدية دون دفعة.");

    private static Error CashboxNotFound(int id) =>
        Error.NotFound(
            "Invoices.CashboxNotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.",
            nameof(InvoiceRequest.CashboxId));

    private static Error CashboxInactive() =>
        Error.Conflict(
            "Invoices.CashboxInactive",
            "لا يمكن استخدام صندوق نقدية غير نشط.");

    private static Error PaymentCurrencyMismatch() =>
        Error.Conflict(
            "Invoices.PaymentCurrencyMismatch",
            "عملة صندوق النقدية لا تتطابق مع عملة الفاتورة.",
            nameof(InvoiceRequest.CashboxId));

    private static Error CashMovementTypeNotFound(int id) =>
        Error.NotFound(
            "Invoices.CashMovementTypeNotFound",
            $"لم يتم العثور على نوع الحركة النقدية رقم {id}.",
            nameof(InvoiceRequest.CashMovementTypeId));

    private static Error CashMovementTypeInactive() =>
        Error.Conflict(
            "Invoices.CashMovementTypeInactive",
            "لا يمكن استخدام نوع حركة نقدية غير نشط.");

    private static Error CashMovementTypeDirectionMismatch() =>
        Error.Conflict(
            "Invoices.CashMovementTypeDirectionMismatch",
            "اتجاه نوع الحركة النقدية لا يتطابق مع نوع الفاتورة.",
            nameof(InvoiceRequest.CashMovementTypeId));

    private static Error CashMovementTypePartnerEffectMismatch() =>
        Error.Conflict(
            "Invoices.CashMovementTypePartnerEffectMismatch",
            "نوع الحركة النقدية لا يتطابق مع أثر الفاتورة على حساب الشريك.",
            nameof(InvoiceRequest.CashMovementTypeId));

    private static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "Invoices.InsufficientCashboxBalance",
            $"الرصيد المتاح في صندوق النقدية رقم {cashboxId} لا يسمح بهذه الدفعة.");

    private sealed record PreparedInvoice(
        CurrencyCode Currency,
        IReadOnlyDictionary<int, int> ItemUnitIds);
}
