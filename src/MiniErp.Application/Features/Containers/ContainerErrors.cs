using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Containers;

public static class ContainerErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "Containers.InvalidId",
            "يجب أن يكون رقم العبوة أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Containers.NotFound",
            $"لم يتم العثور على العبوة رقم {id}.");

    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Containers.CodeExists",
            $"كود العبوة '{code}' مستخدم بالفعل في عبوة نشطة.",
            nameof(ContainerRequest.Code));

    public static Error HasStoreAssignments() =>
        Error.Conflict(
            "Containers.HasStoreAssignments",
            "لا يمكن حذف العبوة لارتباطها بمخزن عبوات حالي أو تاريخي.");
}
