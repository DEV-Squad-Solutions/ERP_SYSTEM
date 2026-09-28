using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Dashboard;

public static class DashboardErrors
{
    public static Error FiscalYearNotFound() =>
        Error.NotFound(
            "Dashboard.FiscalYearNotFound",
            "لا توجد سنة مالية حالية للشركة.");

    public static Error InvalidDateRange() =>
        Error.Validation(
            "Dashboard.InvalidDateRange",
            "فترة لوحة التحكم يجب أن تقع داخل السنة المالية الحالية وألا تزيد على 366 يومًا.");
}
