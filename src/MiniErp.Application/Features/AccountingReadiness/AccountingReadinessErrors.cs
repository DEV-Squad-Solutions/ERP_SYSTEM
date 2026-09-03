using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.AccountingReadiness;

public static class AccountingReadinessErrors
{
    public static Error InvalidFiscalYearId() =>
        Error.Validation(
            "AccountingReadiness.InvalidFiscalYearId",
            "يجب اختيار سنة مالية صحيحة.",
            "FiscalYearId");

    public static Error FiscalYearNotFound(int fiscalYearId) =>
        Error.NotFound(
            "AccountingReadiness.FiscalYearNotFound",
            $"لم يتم العثور على السنة المالية رقم {fiscalYearId}.");

    public static Error FiscalYearClosed(string fiscalYearName) =>
        Error.Conflict(
            "AccountingReadiness.FiscalYearClosed",
            $"يجب إعادة فتح السنة المالية '{fiscalYearName}' قبل تشغيل ترحيل البيانات القديمة.");
}
