using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using MiniErp.Api.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Tests.Swagger;

public sealed class DriversSwaggerDocumentationTests
{
    [Fact]
    public void GetSelect_DocumentsExpiredDriversAsAvailable()
    {
        var operation = new OpenApiOperation();

        new DriversSwaggerDocumentation().Apply(
            operation,
            Context(nameof(DriversController.GetSelect)));

        Assert.Equal("Drivers_GetSelect", operation.OperationId);
        Assert.Contains("including drivers whose licence has expired", operation.Description);
        Assert.DoesNotContain("expired, and other-company", operation.Description);
    }

    private static OperationFilterContext Context(string methodName)
    {
        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(
                new JsonSerializerOptions()));
        return new OperationFilterContext(
            new ApiDescription(),
            schemaGenerator,
            new SchemaRepository(),
            new OpenApiDocument(),
            typeof(DriversController).GetMethod(methodName)!);
    }
}
