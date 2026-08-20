using System.ComponentModel;
using System.Reflection;

namespace MiniErp.Domain.Enums;

public static class CurrencyCodeExtensions
{
    public static string GetDescription(this CurrencyCode currency)
    {
        var member = typeof(CurrencyCode)
            .GetMember(currency.ToString())
            .SingleOrDefault();

        return member?
            .GetCustomAttribute<DescriptionAttribute>()?
            .Description ?? currency.ToString();
    }
}
