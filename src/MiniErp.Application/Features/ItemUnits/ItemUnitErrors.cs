using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ItemUnits;

public static class ItemUnitErrors
{
    public static Error NameExists(string name) =>
        Error.Conflict(
            "ItemUnits.NameExists",
            $"وحدة الصنف '{name}' موجودة بالفعل.",
            nameof(ItemUnitRequest.Name));

    public static Error Inactive(int id) =>
        Error.Conflict(
            "ItemUnits.Inactive",
            $"وحدة الصنف رقم {id} غير نشطة.");

    public static Error InUse() =>
        Error.Conflict(
            "ItemUnits.InUse",
            "لا يمكن حذف وحدة الصنف لارتباطها بأصناف أو مستندات أو حركات حالية أو تاريخية.");

    public static Error InvalidId() =>
        Error.Validation("ItemUnits.InvalidId", "يجب أن يكون رقم وحدة الصنف أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "ItemUnits.NotFound",
            $"لم يتم العثور على وحدة الصنف رقم {id}.");
}
