using Microsoft.OpenApi;
using MiniErp.Api.Errors;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class UnifiedErrorResponseSwaggerFilter : IOperationFilter
{
    private const string ProblemContentType = "application/problem+json";

    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (operation.Responses is null)
        {
            return;
        }

        var errorSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ApiErrorResponse),
            context.SchemaRepository);

        foreach (var response in operation.Responses
                     .Where(entry =>
                         int.TryParse(entry.Key, out var statusCode) &&
                         statusCode >= StatusCodes.Status400BadRequest)
                     .Select(entry => entry.Value))
        {
            response.Content?.Clear();
            response.Content?.Add(
                ProblemContentType,
                new OpenApiMediaType
                {
                    Schema = errorSchema
                });
        }
    }
}
