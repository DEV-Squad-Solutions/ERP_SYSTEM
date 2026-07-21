using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class EnumRequestOperationDocumentationFilter : IOperationFilter
{
    private const int MaximumTraversalDepth = 5;

    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var bodyType = context.ApiDescription.ParameterDescriptions
            .FirstOrDefault(parameter => parameter.Source == BindingSource.Body)
            ?.ModelMetadata
            .ModelType;
        if (bodyType is null)
        {
            return;
        }

        var enumProperties = FindEnumProperties(
            bodyType,
            prefix: null,
            depth: 0,
            new HashSet<Type>());
        if (enumProperties.Count == 0)
        {
            return;
        }

        var lines = enumProperties.Select(enumProperty =>
            $"- `{enumProperty.Path}` (`{enumProperty.EnumType.Name}`): " +
            EnumDocumentationFormatter.FormatValues(enumProperty.EnumType));
        var documentation =
            "**Enum request values**\n\n" +
            string.Join("\n", lines) +
            "\n\nSend enum names as JSON strings; numeric values are documentation references only.";

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? documentation
            : $"{operation.Description.TrimEnd()}\n\n{documentation}";
    }

    private static IReadOnlyList<EnumProperty> FindEnumProperties(
        Type type,
        string? prefix,
        int depth,
        HashSet<Type> traversalPath)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (depth > MaximumTraversalDepth || IsTerminalType(type))
        {
            return [];
        }

        var collectionElementType = GetCollectionElementType(type);
        if (collectionElementType is not null)
        {
            return FindEnumProperties(
                collectionElementType,
                prefix is null ? "[]" : $"{prefix}[]",
                depth + 1,
                traversalPath);
        }

        if (!traversalPath.Add(type))
        {
            return [];
        }

        var results = new List<EnumProperty>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ??
                               property.PropertyType;
            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                           JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var propertyPath = prefix is null ? jsonName : $"{prefix}.{jsonName}";

            if (propertyType.IsEnum)
            {
                results.Add(new EnumProperty(propertyPath, propertyType));
                continue;
            }

            results.AddRange(FindEnumProperties(
                propertyType,
                propertyPath,
                depth + 1,
                traversalPath));
        }

        traversalPath.Remove(type);
        return results;
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type.GetInterfaces()
            .FirstOrDefault(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static bool IsTerminalType(Type type) =>
        type.IsEnum ||
        type.IsPrimitive ||
        type == typeof(string) ||
        type == typeof(decimal) ||
        type == typeof(DateOnly) ||
        type == typeof(TimeOnly) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) ||
        type == typeof(Guid);

    private sealed record EnumProperty(string Path, Type EnumType);
}
