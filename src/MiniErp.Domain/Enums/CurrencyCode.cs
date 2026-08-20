using System.ComponentModel;

namespace MiniErp.Domain.Enums;

public enum CurrencyCode
{
    [Description("الجنيه المصري")]
    EGP = 1,

    [Description("الدولار الأمريكي")]
    USD = 2,

    [Description("اليورو")]
    EUR = 3,

    [Description("الجنيه الإسترليني")]
    GBP = 4,

    [Description("الريال السعودي")]
    SAR = 5,

    [Description("الدرهم الإماراتي")]
    AED = 6,

    [Description("الدينار الكويتي")]
    KWD = 7
}
