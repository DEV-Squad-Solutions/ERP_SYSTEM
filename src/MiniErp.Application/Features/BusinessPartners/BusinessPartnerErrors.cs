using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.BusinessPartners;

public static class BusinessPartnerErrors
{
    public static Error NameExists(string name) =>
        Error.Conflict(
            "BusinessPartners.NameExists",
            $"اسم العميل أو المورد '{name}' موجود بالفعل.",
            nameof(BusinessPartnerRequest.Name));

    public static Error CodeExists(string code) =>
        Error.Conflict(
            "BusinessPartners.CodeExists",
            $"كود العميل أو المورد '{code}' مستخدم بالفعل.",
            "Code");

    public static Error TaxNumberExists() =>
        Error.Conflict(
            "BusinessPartners.TaxNumberExists",
            "يوجد عميل أو مورد آخر يحمل الرقم الضريبي نفسه.",
            nameof(BusinessPartnerRequest.TaxNumber));

    public static Error InvalidId() =>
        Error.Validation(
            "BusinessPartners.InvalidId",
            "يجب أن يكون رقم العميل أو المورد أكبر من صفر.");

    public static Error ContainerStoreNotFound(int id) =>
        Error.NotFound(
            "BusinessPartners.ContainerStoreNotFound",
            $"لم يتم العثور على مخزن عبوات نشط مرتبط بالعميل أو المورد رقم {id}.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "BusinessPartners.NotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.");

    public static Error HasContainerStores() =>
        Error.Conflict(
            "BusinessPartners.HasContainerStores",
            "لا يمكن حذف العميل أو المورد لارتباطه بمخزن عبوات حالي أو تاريخي.");

    public static Error HasFinancialRecords() =>
        Error.Conflict(
            "BusinessPartners.HasFinancialRecords",
            "لا يمكن حذف العميل أو المورد لارتباطه بسجلات مالية حالية أو تاريخية.");

    public static Error CurrencyChangeNotAllowed() =>
        Error.Conflict(
            "BusinessPartners.CurrencyChangeNotAllowed",
            "لا يمكن تغيير عملة العميل أو المورد بعد إنشاء سجلات مالية مرتبطة به.",
            nameof(BusinessPartnerRequest.Currency));
}
