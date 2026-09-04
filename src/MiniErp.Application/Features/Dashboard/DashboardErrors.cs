using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Dashboard;

public static class DashboardErrors
{
    public static Error FiscalYearNotFound() =>
        Error.NotFound(
            "Dashboard.FiscalYearNotFound",
            "لا توجد سنة مالية تغطي فترة لوحة التحكم.");

    public static Error InvalidDateRange() =>
        Error.Validation(
            "Dashboard.InvalidDateRange",
            "يجب أن يكون تاريخ النهاية بعد تاريخ البداية وألا تزيد الفترة على 366 يومًا.");
}
