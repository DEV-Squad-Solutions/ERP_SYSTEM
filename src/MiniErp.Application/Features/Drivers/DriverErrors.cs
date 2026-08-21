using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Drivers;

public static class DriverErrors
{
    public static Error NameExists(string name) =>
        Error.Conflict(
            "Drivers.NameExists",
            $"اسم السائق '{name}' موجود بالفعل.",
            nameof(DriverRequest.Name));

    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Drivers.CodeExists",
            $"كود السائق '{code}' مستخدم بالفعل.",
            "Code");

    public static Error LicenseNumberExists(string licenseNumber) =>
        Error.Conflict(
            "Drivers.LicenseNumberExists",
            $"رقم رخصة السائق '{licenseNumber}' مستخدم بالفعل.",
            nameof(DriverRequest.LicenseNumber));

    public static Error NationalIdExists() =>
        Error.Conflict(
            "Drivers.NationalIdExists",
            "يوجد سائق آخر يحمل الرقم القومي نفسه.",
            nameof(DriverRequest.NationalId));

    public static Error PhoneNumberExists(string phoneNumber) =>
        Error.Conflict(
            "Drivers.PhoneNumberExists",
            $"رقم هاتف السائق '{phoneNumber}' مستخدم بالفعل.",
            nameof(DriverRequest.PhoneNumber));

    public static Error InvalidId() =>
        Error.Validation(
            "Drivers.InvalidId",
            "يجب أن يكون رقم السائق أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Drivers.NotFound",
            $"لم يتم العثور على السائق رقم {id}.");

    public static Error HasDependencies() =>
        Error.Conflict(
            "Drivers.HasDependencies",
            "لا يمكن حذف السائق لارتباطه بفواتير أو رحلات أو سندات نقدية حالية أو تاريخية.");
}
