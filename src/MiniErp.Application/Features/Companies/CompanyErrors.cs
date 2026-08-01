using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Companies;

public static class CompanyErrors
{
    public static Error HasDependencies(string dependency) =>
        Error.Conflict(
            "Companies.HasDependencies",
            $"The company cannot be deleted because related {dependency} records exist, including active or historical data.");

    public static Error CommercialRegisterExists(string commercialRegister) =>
        Error.Conflict(
            "Companies.CommercialRegisterExists",
            $"السجل التجاري '{commercialRegister}' مستخدم بالفعل.",
            nameof(CompanyRequest.CommercialRegister));

    public static Error TaxNumberExists(string taxNumber) =>
        Error.Conflict(
            "Companies.TaxNumberExists",
            $"الرقم الضريبي '{taxNumber}' مستخدم بالفعل.",
            nameof(CompanyRequest.TaxNumber));

    public static Error BaseCurrencyLocked() =>
        Error.Conflict(
            "Companies.BaseCurrencyLocked",
            "لا يمكن تغيير عملة الشركة الأساسية بعد وجود حركات مالية أو مخزنية.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "Companies.RowVersionRequired",
            "The current company rowVersion is required.",
            nameof(CompanyUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "Companies.Concurrency",
            "The company was changed by another user. Reload it and try again.");

    public static Error InvalidId() =>
        Error.Validation(
            "Companies.InvalidId",
            "يجب أن يكون رقم الشركة أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Companies.NotFound",
            $"لم يتم العثور على الشركة رقم {id}.");
}
