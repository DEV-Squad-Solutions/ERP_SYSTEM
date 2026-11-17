using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ContainerMovements;

public static class ContainerMovementErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "ContainerMovements.InvalidId",
            "يجب أن يكون رقم الحركة أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "ContainerMovements.NotFound",
            $"لم يتم العثور على حركة العبوة رقم {id}.");

    public static Error InvalidContainerStoreId() =>
        Error.Validation(
            "ContainerMovements.InvalidContainerStoreId",
            "يجب أن يكون رقم مخزن العبوات أكبر من صفر.",
            nameof(ContainerMovementRequest.ContainerStoreId));

    public static Error InvalidContainerId() =>
        Error.Validation(
            "ContainerMovements.InvalidContainerId",
            "يجب أن يكون رقم العبوة أكبر من صفر.",
            nameof(ContainerMovementRequest.ContainerId));

    public static Error ContainerStoreNotFound(int id) =>
        Error.NotFound(
            "ContainerMovements.ContainerStoreNotFound",
            $"لم يتم العثور على مخزن العبوات رقم {id}.",
            nameof(ContainerMovementRequest.ContainerStoreId));

    public static Error ContainerStoreNotContainerStore() =>
        Error.Conflict(
            "ContainerMovements.ContainerStoreNotContainerStore",
            "يجب أن يكون المخزن المختار مخزن عبوات.",
            nameof(ContainerMovementRequest.ContainerStoreId));

    public static Error ContainerStoreInactive() =>
        Error.Conflict(
            "ContainerMovements.ContainerStoreInactive",
            "لا يمكن تسجيل حركة على مخزن عبوات غير نشط.",
            nameof(ContainerMovementRequest.ContainerStoreId));

    public static Error ContainerStoreWithoutPartner() =>
        Error.Conflict(
            "ContainerMovements.ContainerStoreWithoutPartner",
            "مخزن العبوات غير مرتبط بعميل أو مورد.",
            nameof(ContainerMovementRequest.ContainerStoreId));

    public static Error ContainerNotFound(int id) =>
        Error.NotFound(
            "ContainerMovements.ContainerNotFound",
            $"لم يتم العثور على العبوة رقم {id}.",
            nameof(ContainerMovementRequest.ContainerId));

    public static Error ContainerInactive() =>
        Error.Conflict(
            "ContainerMovements.ContainerInactive",
            "لا يمكن تسجيل حركة على عبوة غير نشطة.",
            nameof(ContainerMovementRequest.ContainerId));

    public static Error ContainerNotAssigned() =>
        Error.Conflict(
            "ContainerMovements.ContainerNotAssigned",
            "العبوة غير مخصصة لهذا المخزن. أضفها من إدارة العبوات أولًا.",
            nameof(ContainerMovementRequest.ContainerId));

    public static Error InvalidMovementDate() =>
        Error.Validation(
            "ContainerMovements.InvalidMovementDate",
            "تاريخ الحركة مطلوب.",
            nameof(ContainerMovementRequest.MovementDate));

    public static Error UnitsNegative() =>
        Error.Validation(
            "ContainerMovements.UnitsNegative",
            "لا يمكن أن يكون عدد الوحدات الصادرة أو الواردة سالبًا.",
            nameof(ContainerMovementRequest.OutgoingUnits));

    public static Error UnitsRequired() =>
        Error.Validation(
            "ContainerMovements.UnitsRequired",
            "يجب إدخال عدد صادر أو عدد وارد أكبر من صفر.",
            nameof(ContainerMovementRequest.OutgoingUnits));

    public static Error DescriptionTooLong() =>
        Error.Validation(
            "ContainerMovements.DescriptionTooLong",
            $"يجب ألا يزيد البيان عن " +
            $"{ContainerMovementRequest.DescriptionMaximumLength} حرف.",
            nameof(ContainerMovementRequest.Description));

    public static Error RowVersionRequired() =>
        Error.Validation(
            "ContainerMovements.RowVersionRequired",
            "بصمة السجل مطلوبة. أعد تحميل البيانات ثم حاول مرة أخرى.",
            nameof(ContainerMovementUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "ContainerMovements.Concurrency",
            "تم تعديل الحركة بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error InvoiceGeneratedReadOnly() =>
        Error.Conflict(
            "ContainerMovements.InvoiceGeneratedReadOnly",
            "حركات العبوات الناتجة من الفواتير لا يمكن تعديلها أو حذفها من هنا. عدّل الفاتورة نفسها.");
}
