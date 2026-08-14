using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Items;

public static class ItemErrors
{
    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Items.CodeExists",
            $"كود الصنف '{code}' مستخدم بالفعل.",
            "Code");

    public static Error InvalidId() =>
        Error.Validation("Items.InvalidId", "يجب أن يكون رقم الصنف أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound("Items.NotFound", $"لم يتم العثور على الصنف رقم {id}.");

    public static Error InUse() =>
        Error.Conflict(
            "Items.InUse",
            "لا يمكن حذف الصنف لارتباطه بمستندات أو حركات حالية أو تاريخية.");
}
