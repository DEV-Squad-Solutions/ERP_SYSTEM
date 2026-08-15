using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public static class PartnerOpeningBalanceErrors
{
    public static Error RowVersionRequired() =>
        Error.Validation(
            "PartnerOpeningBalances.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي للتعديل.",
            nameof(PartnerOpeningBalanceUpdateRequest.RowVersion));

    public static Error BusinessPartnerNotFound(int id) =>
        Error.NotFound(
            "PartnerOpeningBalances.BusinessPartnerNotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.",
            nameof(PartnerOpeningBalanceRequest.BusinessPartnerId));

    public static Error BusinessPartnerInactive() =>
        Error.Conflict(
            "PartnerOpeningBalances.BusinessPartnerInactive",
            "لا يمكن استخدام عميل أو مورد غير نشط.",
            nameof(PartnerOpeningBalanceRequest.BusinessPartnerId));

    public static Error CurrencyMismatch() =>
        Error.Conflict(
            "PartnerOpeningBalances.CurrencyMismatch",
            "يجب أن تطابق عملة رصيد الشريك عملة العميل أو المورد.",
            nameof(PartnerOpeningBalanceRequest.Currency));

    public static Error InvalidId() =>
        Error.Validation(
            "PartnerOpeningBalances.InvalidId",
            "يجب أن يكون رقم رصيد الشريك أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "PartnerOpeningBalances.NotFound",
            $"لم يتم العثور على رصيد الشريك رقم {id}.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "PartnerOpeningBalances.DocumentNumberExists",
            $"رقم المستند '{number}' مستخدم بالفعل.",
            "DocumentNumber");

    public static Error Concurrency() =>
        Error.Conflict(
            "PartnerOpeningBalances.Concurrency",
            "تم تعديل رصيد الشريك بواسطة عملية أخرى. أعد تحميل المستند ثم حاول مرة أخرى.");
}
