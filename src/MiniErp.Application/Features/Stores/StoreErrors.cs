using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Stores;

public static class StoreErrors
{
    public static Error ProductStoreBusinessPartner() =>
        Error.Validation(
            "Stores.ProductStoreBusinessPartner",
            "يجب عدم تحديد عميل أو مورد لمخزن المنتجات.");

    public static Error InvalidBusinessPartnerId() =>
        Error.Validation(
            "Stores.InvalidBusinessPartnerId",
            "يجب تحديد عميل أو مورد صحيح للمخزن المخصص للعبوات.");

    public static Error BusinessPartnerNotFound(int id) =>
        Error.NotFound(
            "Stores.BusinessPartnerNotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.");

    public static Error BusinessPartnerInactive() =>
        Error.Conflict(
            "Stores.BusinessPartnerInactive",
            "يجب ربط مخزن العبوات بعميل أو مورد نشط.");

    public static Error InvalidId() =>
        Error.Validation("Stores.InvalidId", "يجب أن يكون رقم المخزن أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound("Stores.NotFound", $"لم يتم العثور على المخزن رقم {id}.");

    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Stores.CodeExists",
            $"كود المخزن '{code}' مستخدم بالفعل.",
            "Code");

    public static Error ActiveContainerStoreExists(int businessPartnerId) =>
        Error.Conflict(
            "Stores.ActiveContainerStoreExists",
            $"يوجد بالفعل مخزن عبوات نشط مخصص للعميل أو المورد رقم {businessPartnerId}.",
            nameof(StoreRequest.BusinessPartnerId));

    public static Error HasContainerAssignments() =>
        Error.Conflict(
            "Stores.HasContainerAssignments",
            "لا يمكن حذف مخزن العبوات أو تغيير نوعه أو العميل أو المورد المرتبط به لوجود ربط عبوات حالي أو تاريخي.");

    public static Error HasDependencies() =>
        Error.Conflict(
            "Stores.HasDependencies",
            "لا يمكن حذف المخزن لارتباطه بمستندات أو حركات حالية أو تاريخية.");

    public static Error HistoricalIdentityChangeNotAllowed() =>
        Error.Conflict(
            "Stores.HistoricalIdentityChangeNotAllowed",
            "لا يمكن تغيير نوع المخزن أو العميل أو المورد المرتبط به لوجود مستندات أو حركات حالية أو تاريخية.");
}
