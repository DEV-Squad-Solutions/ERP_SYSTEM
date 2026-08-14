using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public static class ExchangeRateErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "ExchangeRates.InvalidId",
            "يجب أن يكون رقم سعر الصرف أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "ExchangeRates.NotFound",
            $"لم يتم العثور على سعر الصرف رقم {id}.");

    public static Error InvalidCurrency() =>
        Error.Validation(
            "ExchangeRates.InvalidCurrency",
            "العملة المحددة غير صالحة.",
            nameof(ExchangeRateRequest.Currency));

    public static Error InvalidRate() =>
        Error.Validation(
            "ExchangeRates.InvalidRate",
            "يجب أن يكون سعر الصرف أكبر من صفر وألا يتجاوز 12 منزلة عشرية.",
            nameof(ExchangeRateRequest.Rate));

    public static Error BaseCurrencyRateNotAllowed() =>
        Error.Validation(
            "ExchangeRates.BaseCurrencyRateNotAllowed",
            "لا يتم إنشاء سعر صرف لعملة الشركة الأساسية؛ سعرها يساوي واحدًا دائمًا.",
            nameof(ExchangeRateRequest.Currency));

    public static Error BaseCurrencyRateMustBeOne() =>
        Error.Validation(
            "ExchangeRates.BaseCurrencyRateMustBeOne",
            "يجب أن يساوي سعر صرف عملة الشركة الأساسية واحدًا.",
            "exchangeRate");

    public static Error Missing(
        CurrencyCode currency,
        DateOnly date) =>
        Error.Validation(
            "ExchangeRates.Missing",
            $"لا يوجد سعر صرف للعملة {currency} بتاريخ {date:yyyy-MM-dd} أو قبله.",
            "exchangeRate");

    public static Error Duplicate() =>
        Error.Conflict(
            "ExchangeRates.Duplicate",
            "يوجد سعر صرف نشط لهذه العملة في التاريخ نفسه.",
            nameof(ExchangeRateRequest.RateDate));

    public static Error Referenced() =>
        Error.Conflict(
            "ExchangeRates.Referenced",
            "لا يمكن تعديل أو حذف سعر صرف مستخدم في مستند مالي. أضف سعرًا بتاريخ جديد بدلًا من ذلك.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "ExchangeRates.RowVersionRequired",
            "يجب إرسال إصدار سعر الصرف الحالي.",
            nameof(ExchangeRateUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "ExchangeRates.Concurrency",
            "تم تعديل سعر الصرف بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error CompanySettingsNotFound() =>
        Error.NotFound(
            "ExchangeRates.CompanySettingsNotFound",
            "Company exchange-rate settings were not found.");
}
