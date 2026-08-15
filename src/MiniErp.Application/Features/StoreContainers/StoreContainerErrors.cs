using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StoreContainers;

public static class StoreContainerErrors
{
    public static Error ContainerNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "StoreContainers.ContainerNotFound",
            $"لم يتم العثور على العبوات ذات الأرقام: {string.Join(", ", ids)}.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error ContainerInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StoreContainers.ContainerInactive",
            $"يجب اختيار عبوات نشطة. العبوات غير النشطة: {string.Join(", ", ids)}.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error StoreNotFound(int id) =>
        Error.NotFound(
            "StoreContainers.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            nameof(StoreContainerUpsertRequest.StoreId));

    public static Error StoreNotContainerStore() =>
        Error.Conflict(
            "StoreContainers.StoreNotContainerStore",
            "يجب اختيار مخزن عبوات وليس مخزن منتجات.",
            nameof(StoreContainerUpsertRequest.StoreId));

    public static Error StoreInactive() =>
        Error.Conflict(
            "StoreContainers.StoreInactive",
            "يجب اختيار مخزن عبوات نشط.",
            nameof(StoreContainerUpsertRequest.StoreId));

    public static Error StoreBusinessPartnerInactive() =>
        Error.Conflict(
            "StoreContainers.StoreBusinessPartnerInactive",
            "يجب أن يكون العميل أو المورد المرتبط بمخزن العبوات نشطًا.",
            nameof(StoreContainerUpsertRequest.StoreId));

    public static Error InvalidId() =>
        Error.Validation(
            "StoreContainers.InvalidId",
            "يجب أن يكون رقم ربط العبوة بالمخزن أكبر من صفر.");

    public static Error InvalidStoreId() =>
        Error.Validation(
            "StoreContainers.InvalidStoreId",
            "يجب أن يكون رقم المخزن أكبر من صفر.",
            nameof(StoreContainerUpsertRequest.StoreId));

    public static Error ContainerIdsRequired() =>
        Error.Validation(
            "StoreContainers.ContainerIdsRequired",
            "حقل العبوات مطلوب.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error TooManyContainers() =>
        Error.Validation(
            "StoreContainers.TooManyContainers",
            $"يجب ألا يزيد عدد العبوات عن " +
            $"{StoreContainerUpsertRequest.MaximumContainerCount}.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error InvalidContainerId() =>
        Error.Validation(
            "StoreContainers.InvalidContainerId",
            "يجب أن تكون جميع أرقام العبوات أكبر من صفر.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error DuplicateContainerIds() =>
        Error.Validation(
            "StoreContainers.DuplicateContainerIds",
            "يجب عدم تكرار رقم العبوة في القائمة.",
            nameof(StoreContainerUpsertRequest.ContainerIds));

    public static Error NotFound(int id) =>
        Error.NotFound(
            "StoreContainers.NotFound",
            $"لم يتم العثور على ربط العبوة بالمخزن رقم {id}.");
}
