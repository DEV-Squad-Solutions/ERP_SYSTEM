using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverTrips;

public static class DriverTripErrors
{
    public static Error InvalidItems() =>
        Error.Validation(
            "DriverTrips.InvalidCostItems",
            "بيانات تكاليف رحلات السائقين غير صالحة.");

    public static Error DuplicateIds() =>
        Error.Validation(
            "DriverTrips.DuplicateIds",
            "لا يجوز تكرار رقم رحلة السائق داخل الطلب.",
            nameof(DriverTripBulkCostUpdateRequest.Items));

    public static Error TripsNotFound() =>
        Error.NotFound(
            "DriverTrips.NotFound",
            "تعذر العثور على رحلة أو أكثر داخل الشركة الحالية.");

    public static Error Concurrency(int driverTripId) =>
        Error.Conflict(
            "DriverTrips.Concurrency",
            $"تم تعديل رحلة السائق رقم {driverTripId} بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
