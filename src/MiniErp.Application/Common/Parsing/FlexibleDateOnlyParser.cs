using System.Globalization;

namespace MiniErp.Application.Common.Parsing;

public static class FlexibleDateOnlyParser
{
    private static readonly string[] ExactFormats =
    [
        "yyyy-M-d",
        "yyyy/M/d",
        "yyyy.M.d",
        "d/M/yyyy",
        "d-M-yyyy",
        "d.M.yyyy",
        "M/d/yyyy",
        "M-d-yyyy",
        "M.d.yyyy",
        "yyyyMMdd",
        "ddMMyyyy"
    ];

    private static readonly CultureInfo[] Cultures =
    [
        CultureInfo.GetCultureInfo("ar-EG"),
        CultureInfo.GetCultureInfo("en-GB"),
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en-US")
    ];

    public static bool TryParse(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeDigits(value.Trim());
        if (DateOnly.TryParseExact(
                normalized,
                ExactFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }

        foreach (var culture in Cultures)
        {
            if (DateOnly.TryParse(
                    normalized,
                    culture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out date))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeDigits(string value)
    {
        var characters = value.ToCharArray();
        var changed = false;

        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (character is >= '\u0660' and <= '\u0669')
            {
                characters[index] = (char)('0' + character - '\u0660');
                changed = true;
            }
            else if (character is >= '\u06F0' and <= '\u06F9')
            {
                characters[index] = (char)('0' + character - '\u06F0');
                changed = true;
            }
        }

        return changed
            ? new string(characters)
            : value;
    }
}
