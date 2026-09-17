using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniErp.Application.Common.Parsing;

namespace MiniErp.Api.Serialization;

/// <summary>
/// Deserializes <see cref="DateOnly"/> from the same flexible formats supported by
/// <see cref="FlexibleDateOnlyParser"/> (e.g. "yyyy-MM-dd", Arabic digits) PLUS ISO-8601
/// datetime strings such as "2026-09-17T00:00:00Z" that Swagger/frontend clients may send.
/// Always serializes as "yyyy-MM-dd".
/// </summary>
public sealed class FlexibleDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var str = reader.GetString();

        // 1. Try the existing application-level parser (handles yyyy-M-d, yyyy/M/d, Arabic digits, etc.)
        if (FlexibleDateOnlyParser.TryParse(str, out var date))
            return date;

        // 2. Fallback: treat as ISO-8601 datetime string (e.g. "2026-09-17T00:00:00Z" from Swagger)
        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return DateOnly.FromDateTime(dt);

        throw new JsonException($"The JSON value '{str}' could not be converted to DateOnly.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

/// <summary>
/// Nullable wrapper around <see cref="FlexibleDateOnlyJsonConverter"/>.
/// Passes <see langword="null"/> JSON tokens through unchanged.
/// </summary>
public sealed class FlexibleNullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private static readonly FlexibleDateOnlyJsonConverter Inner = new();

    public override DateOnly? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return Inner.Read(ref reader, typeof(DateOnly), options);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly? value,
        JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            Inner.Write(writer, value.Value, options);
    }
}
