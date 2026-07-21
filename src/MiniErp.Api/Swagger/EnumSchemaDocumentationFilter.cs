using System.Globalization;
using Microsoft.OpenApi;
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

        var enumDocumentation =
            $"Accepted JSON names: {EnumDocumentationFormatter.FormatValues(enumType)}. " +
            "Send the name; numeric values are documentation references only.";

        openApiSchema.Description = string.IsNullOrWhiteSpace(openApiSchema.Description)
            ? enumDocumentation
            : $"{openApiSchema.Description.TrimEnd()}\n\n{enumDocumentation}";
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
