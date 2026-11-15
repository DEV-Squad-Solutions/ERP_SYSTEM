using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverTrips;

public static class DriverTripErrors
{
    public static Error InvalidRequest() => Error.Validation("DriverTrips.InvalidRequest", "بيانات رحلة السائق غير صالحة.");
    public static Error RowVersionRequired() => Error.Validation("DriverTrips.RowVersionRequired", "يجب إرسال إصدار رحلة السائق الحالي.", "RowVersion");
    public static Error InvoiceOwned() => Error.Conflict("DriverTrips.InvoiceOwned", "رحلة الفاتورة تدار من خلال الفاتورة أو شاشة تكاليف الرحلات.");
    public static Error LinkedVoucher() => Error.Conflict("DriverTrips.LinkedVoucher", "توجد سندات حالية أو تاريخية مرتبطة بالرحلة؛ لا يمكن حذفها أو تغيير السائق أو الطرف أو التاريخ.");
    public static Error FiscalYearImmutable() => Error.Validation("DriverTrips.FiscalYearImmutable", "لا يمكن تغيير السنة المالية للرحلة.", "FiscalYearId");
    public static Error DriverUnavailable() => Error.Validation("DriverTrips.DriverUnavailable", "يجب اختيار سائق نشط داخل الشركة الحالية.", "DriverId");
    public static Error PartnerUnavailable() => Error.Validation("DriverTrips.PartnerUnavailable", "يجب اختيار طرف نشط داخل الشركة الحالية.", "BusinessPartnerId");
    public static Error PostingUnavailable() => Error.Conflict("DriverTrips.PostingUnavailable", "خدمة ترحيل تكاليف الرحلات غير متاحة.");

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

    public static Error Concurrency(int? driverTripId = null) =>
        Error.Conflict(
            "DriverTrips.Concurrency",
            driverTripId.HasValue
                ? $"تم تعديل رحلة السائق رقم {driverTripId.Value} بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى."
                : "تعارض التعديل مع عملية أخرى على رحلات السائقين. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
