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
    public void PartySelect_DocumentsScopeShapeOrderingAndExpiredDrivers()
    {
        var operation = new OpenApiOperation();

        new CashVouchersSwaggerDocumentation().Apply(
            operation,
            Context(nameof(CashVouchersController.GetPartySelect)));

        Assert.Equal(
            "CashVouchers_GetPartySelect",
            operation.OperationId);
        Assert.Contains("selected company", operation.Description);
        Assert.Contains("expenses", operation.Description);
        Assert.Contains("revenues", operation.Description);
        Assert.Contains("id, name, classification, code, and accountType", operation.Description);
        Assert.Contains("Expense accounts", operation.Description);
        Assert.Contains("Payment vouchers", operation.Description);
        Assert.Contains("Receipt vouchers", operation.Description);
        Assert.Contains("ordered by name then id", operation.Description);
        Assert.Contains("license is expired", operation.Description);
        Assert.Contains("inactive", operation.Description);
        Assert.DoesNotContain("cashMovements", operation.Description);
        Assert.DoesNotContain("direction", operation.Description);
    }

    [Fact]
    public void PartySelectSchema_UsesSeparatedCamelCaseAccountArrays()
    {
        var generator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var repository = new SchemaRepository();

        generator.GenerateSchema(
            typeof(CashVoucherPartySelectResponse),
            repository);

        var responseSchema = repository.Schemas[
            nameof(CashVoucherPartySelectResponse)];
        var responseProperties = Assert.IsAssignableFrom<
            IDictionary<string, IOpenApiSchema>>(responseSchema.Properties);
        Assert.Equal(
            [
                "businessPartners",
                "drivers",
                "employees",
                "expenses",
                "revenues"
            ],
            responseProperties.Keys);
        Assert.DoesNotContain("cashMovements", responseProperties.Keys);

        var accountSchema = repository.Schemas[
            nameof(CashVoucherAccountSelectResponse)];
        var accountProperties = Assert.IsAssignableFrom<
            IDictionary<string, IOpenApiSchema>>(accountSchema.Properties);
        Assert.Equal(
            ["id", "name", "classification", "code", "accountType"],
            accountProperties.Keys);
        Assert.DoesNotContain("direction", accountProperties.Keys);
    }

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
        Assert.Contains("cashMovementTypeId is optional", operation.Description);
        Assert.Contains("completed, posted voucher", operation.Description);
        Assert.Contains("preserves request order", operation.Description);
        Assert.DoesNotContain("clientKey", operation.Description);
    }

    [Fact]
    public void Update_DocumentsExclusiveTargetAndManualMovementEligibility()
    {
        var operation = new OpenApiOperation();

        new CashVouchersSwaggerDocumentation().Apply(
            operation,
            Context(nameof(CashVouchersController.Update)));

        Assert.Contains("exactly one posting target", operation.Description);
        Assert.Contains("cashMovementTypeId to be null", operation.Description);
        Assert.Contains("completed, posted voucher", operation.Description);
        Assert.Contains("Receipt with Revenue", operation.Description);
        Assert.Contains("Payment with Expense", operation.Description);
        Assert.Contains("non-partner", operation.Description);
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
