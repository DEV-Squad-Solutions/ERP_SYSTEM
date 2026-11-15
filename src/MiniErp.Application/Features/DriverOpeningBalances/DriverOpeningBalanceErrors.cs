using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverOpeningBalances;

public static class DriverOpeningBalanceErrors
{
    public static Error InvalidId() =>
        Error.Validation("DriverOpeningBalances.InvalidId", "رقم مستند الرصيد الافتتاحي غير صالح.");

    public static Error NotFound(int id) =>
        Error.NotFound("DriverOpeningBalances.NotFound", $"لم يتم العثور على رصيد افتتاحي برقم {id}.");

    public static Error DriverNotFound(int driverId) =>
        Error.NotFound("DriverOpeningBalances.DriverNotFound", $"لم يتم العثور على سائق برقم {driverId}.");

    public static Error InactiveDriver() =>
        Error.Conflict("DriverOpeningBalances.InactiveDriver", "يجب أن يكون السائق نشطًا لإضافة رصيد افتتاحي.");

    public static Error FiscalYearClosed(string name) =>
        Error.Conflict("DriverOpeningBalances.FiscalYearClosed", $"السنة المالية '{name}' مغلقة.");

    public static Error Duplicate() =>
        Error.Conflict("DriverOpeningBalances.Duplicate", "يوجد رصيد افتتاحي لهذا السائق في السنة المالية المحددة.");

    public static Error CarriedForwardExists() =>
        Error.Conflict("DriverOpeningBalances.CarriedForwardExists", "يوجد رصيد مرحّل لهذا السائق في السنة المالية المحددة.");

    public static Error LegacyOpeningExists() =>
        Error.Conflict("DriverOpeningBalances.LegacyOpeningExists", "يوجد قيد افتتاحي سابق لهذا السائق في السنة المالية المحددة.");

    public static Error ReadOnly() =>
        Error.Conflict("DriverOpeningBalances.ReadOnly", "الرصيد المرحّل للقراءة فقط.");

    public static Error ScopeImmutable() =>
        Error.Conflict("DriverOpeningBalances.ScopeImmutable", "لا يمكن تغيير السائق أو السنة المالية لمستند رصيد افتتاحي.");

    public static Error Concurrency() =>
        Error.Conflict("DriverOpeningBalances.Concurrency", "تغيّر الرصيد الافتتاحي. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error RowVersionRequired() =>
        Error.Validation("DriverOpeningBalances.RowVersionRequired", "يجب إرسال الإصدار الحالي لمستند الرصيد الافتتاحي بصيغة Base64.", nameof(DriverOpeningBalanceUpdateRequest.RowVersion));

    public static Error InvalidAmount() =>
        Error.Validation("DriverOpeningBalances.InvalidAmount", "يجب أن يكون المبلغ موجبًا، بحد أقصى 15 خانة صحيحة ومنزلتين عشريتين.", nameof(DriverOpeningBalanceRequest.Amount));

    public static Error InvalidBalanceType() =>
        Error.Validation("DriverOpeningBalances.InvalidBalanceType", "نوع رصيد السائق الافتتاحي غير صالح.", nameof(DriverOpeningBalanceRequest.BalanceType));

    public static Error NotesTooLong() =>
        Error.Validation("DriverOpeningBalances.NotesTooLong", "يجب ألا تتجاوز الملاحظات 1000 حرف.", nameof(DriverOpeningBalanceRequest.Notes));
}
