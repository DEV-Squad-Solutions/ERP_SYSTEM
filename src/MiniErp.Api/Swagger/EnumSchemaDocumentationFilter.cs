using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MiniErp.Domain.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class EnumSchemaDocumentationFilter : ISchemaFilter
{
    public void Apply(
        IOpenApiSchema schema,
        SchemaFilterContext context)
    {
        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum || schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        var enumNames = Enum.GetNames(enumType);
        openApiSchema.Type = JsonSchemaType.String;
        openApiSchema.Format = null;
        openApiSchema.Enum = enumNames
            .Select(name => JsonValue.Create(name))
            .Cast<JsonNode>()
            .ToList();
        openApiSchema.Example = JsonValue.Create(enumNames[0]);

        var enumDocumentation =
            $"Accepted values: {EnumDocumentationFormatter.FormatValues(enumType)}. " +
            "Send the enum name as a JSON string or the documented numeric value.";
        var businessMeaning = GetBusinessMeaning(enumType);
        if (businessMeaning is not null)
        {
            enumDocumentation = $"{businessMeaning}\n\n{enumDocumentation}";
        }

        openApiSchema.Description = string.IsNullOrWhiteSpace(openApiSchema.Description)
            ? enumDocumentation
            : $"{openApiSchema.Description.TrimEnd()}\n\n{enumDocumentation}";
    }

    private static string? GetBusinessMeaning(Type enumType)
    {
        if (enumType == typeof(CashDirection))
        {
            return "Receipt increases the cashbox balance. Payment decreases " +
                   "the cashbox balance. In accounting terms, Receipt means " +
                   "Debit Cash and Payment means Credit Cash. This describes " +
                   "the cash side. For a normal partner settlement, the other " +
                   "side is Credit Partner for Receipt and Debit Partner for " +
                   "Payment.";
        }

        if (enumType == typeof(CashPartyType))
        {
            return "Controls the party fields shown by the frontend: None uses " +
                   "no party field; Partner requires businessPartnerId; Driver " +
                   "requires driverId and allows an optional driverTripId; Other " +
                   "requires externalPartyName.";
        }

        return null;
    }
}

internal static class EnumDocumentationFormatter
{
    public static string FormatValues(Type enumType)
    {
        var values = Enum.GetNames(enumType)
            .Select(name =>
            {
                var value = Enum.Parse(enumType, name);
                var numericValue = Convert.ChangeType(
                    value,
                    Enum.GetUnderlyingType(enumType),
                    CultureInfo.InvariantCulture);

                return $"{name} = {Convert.ToString(numericValue, CultureInfo.InvariantCulture)}";
            });

        return string.Join(", ", values);
    }
}
