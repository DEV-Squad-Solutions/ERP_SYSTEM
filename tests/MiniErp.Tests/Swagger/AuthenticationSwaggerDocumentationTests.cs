using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using MiniErp.Api.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Tests.Swagger;

public sealed class AuthenticationSwaggerDocumentationTests
{
    [Fact]
    public void Login_WhenSeedIsEnabled_PrefillsConfiguredAdminCredentials()
    {
        var configuration = Configuration(
            seedEnabled: true,
            password: "P@ssword123");
        var mediaType = new OpenApiMediaType();
        var operation = LoginOperation(mediaType);

        new AuthenticationSwaggerDocumentation(configuration)
            .Apply(operation, LoginContext());

        var example = Assert.IsType<JsonObject>(mediaType.Example);
        Assert.Equal("admin", example["userName"]?.GetValue<string>());
        Assert.Equal("P@ssword123", example["password"]?.GetValue<string>());
    }

    [Fact]
    public void Login_WhenSeedIsDisabled_DoesNotExposeCredentials()
    {
        var configuration = Configuration(
            seedEnabled: false,
            password: "P@ssword123");
        var mediaType = new OpenApiMediaType();
        var operation = LoginOperation(mediaType);

        new AuthenticationSwaggerDocumentation(configuration)
            .Apply(operation, LoginContext());

        Assert.Null(mediaType.Example);
    }

    private static IConfiguration Configuration(
        bool seedEnabled,
        string password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = seedEnabled.ToString(),
                ["Seed:Password"] = password
            })
            .Build();

    private static OpenApiOperation LoginOperation(
        OpenApiMediaType mediaType) =>
        new()
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = mediaType
                }
            }
        };

    private static OperationFilterContext LoginContext()
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
            typeof(AuthController).GetMethod(nameof(AuthController.Login))!);
    }
}
