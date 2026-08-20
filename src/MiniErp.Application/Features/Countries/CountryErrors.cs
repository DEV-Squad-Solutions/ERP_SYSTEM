using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Countries;

public static class CountryErrors
{
    public static Error NameExists(string name) =>
        Error.Conflict(
            "Countries.NameExists",
            $"اسم الدولة '{name}' موجود بالفعل.",
            nameof(CountryRequest.Name));

    public static Error InvalidId() =>
        Error.Validation(
            "Countries.InvalidId",
            "يجب أن يكون رقم الدولة أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Countries.NotFound",
            $"لم يتم العثور على الدولة رقم {id}.");

    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Countries.CodeExists",
            $"كود الدولة '{code}' مستخدم بالفعل في دولة نشطة.",
            "Code");

    public static Error HasInvoices() =>
        Error.Conflict(
            "Countries.HasInvoices",
            "لا يمكن حذف الدولة لارتباطها بفواتير حالية أو تاريخية.");
}
