using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using MiniErp.Api.Swagger;
using MiniErp.Application.Features.CashVouchers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Tests.Swagger;

public sealed class CashVouchersSwaggerDocumentationTests
{
    [Fact]
    public void Bulk_DocumentsAtomicMixedOperations()
    {
        var operation = new OpenApiOperation();

        new CashVouchersSwaggerDocumentation().Apply(
            operation,
            Context(nameof(CashVouchersController.Bulk)));

        Assert.Equal("CashVouchers_Bulk", operation.OperationId);
        Assert.Contains("atomic", operation.Description);
        Assert.Contains("rowVersion", operation.Description);
        Assert.Contains("preserves request order", operation.Description);
        Assert.DoesNotContain("clientKey", operation.Description);
    }

    [Fact]
    public void BulkItemSchema_UsesActionDiscriminatorAndDistinctShapes()
    {
        var options = new SchemaGeneratorOptions
        {
            UseOneOfForPolymorphism = true
        };
        options.SchemaFilters.Add(
            new CashVoucherBulkPolymorphismSchemaFilter());
        var generator = new SchemaGenerator(
            options,
            new JsonSerializerDataContractResolver(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var repository = new SchemaRepository();

        var itemSchema = generator.GenerateSchema(
            typeof(CashVoucherBulkItemRequest),
            repository);

        var oneOf = Assert.IsAssignableFrom<IList<IOpenApiSchema>>(
            itemSchema.OneOf);
        Assert.Equal(3, oneOf.Count);
        var baseSchema = repository.Schemas[
            nameof(CashVoucherBulkItemRequest)];
        var discriminator = Assert.IsType<OpenApiDiscriminator>(
            baseSchema.Discriminator);
        Assert.Equal("action", discriminator.PropertyName);
        var baseRequired = Assert.IsAssignableFrom<ISet<string>>(
            baseSchema.Required);
        Assert.Contains("action", baseRequired);
        var mapping = Assert.IsAssignableFrom<
            IDictionary<string, OpenApiSchemaReference>>(
                discriminator.Mapping);
        Assert.Equal(
            ["Add", "Delete", "Update"],
            mapping.Keys.Order());

        var addSchema = repository.Schemas[
            nameof(CashVoucherBulkAddItemRequest)];
        var addProperties = Assert.IsAssignableFrom<
            IDictionary<string, IOpenApiSchema>>(addSchema.Properties);
        Assert.Contains("voucher", addProperties.Keys);
        Assert.DoesNotContain("id", addProperties.Keys);
        Assert.DoesNotContain("rowVersion", addProperties.Keys);
        var addRequired = Assert.IsAssignableFrom<ISet<string>>(
            addSchema.Required);
        Assert.Contains("voucher", addRequired);

        var updateSchema = repository.Schemas[
            nameof(CashVoucherBulkUpdateItemRequest)];
        var updateProperties = Assert.IsAssignableFrom<
            IDictionary<string, IOpenApiSchema>>(updateSchema.Properties);
        Assert.Contains("id", updateProperties.Keys);
        Assert.Contains("rowVersion", updateProperties.Keys);
        Assert.Contains("voucher", updateProperties.Keys);
        var updateRequired = Assert.IsAssignableFrom<ISet<string>>(
            updateSchema.Required);
        Assert.Contains("id", updateRequired);
        Assert.Contains("rowVersion", updateRequired);
        Assert.Contains("voucher", updateRequired);

        var deleteSchema = repository.Schemas[
            nameof(CashVoucherBulkDeleteItemRequest)];
        var deleteProperties = Assert.IsAssignableFrom<
            IDictionary<string, IOpenApiSchema>>(deleteSchema.Properties);
        Assert.Contains("id", deleteProperties.Keys);
        Assert.Contains("rowVersion", deleteProperties.Keys);
        Assert.DoesNotContain("voucher", deleteProperties.Keys);
        var deleteRequired = Assert.IsAssignableFrom<ISet<string>>(
            deleteSchema.Required);
        Assert.Contains("id", deleteRequired);
        Assert.Contains("rowVersion", deleteRequired);
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
            typeof(CashVouchersController).GetMethod(methodName)!);
    }
}
