using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MiniErp.Application.Features.CashVouchers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CashVoucherBulkPolymorphismSchemaFilter : ISchemaFilter
{
    public void Apply(
        IOpenApiSchema schema,
        SchemaFilterContext context)
    {
        if (context.Type != typeof(CashVoucherBulkItemRequest) ||
            schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        openApiSchema.Discriminator = new OpenApiDiscriminator
        {
            PropertyName = "action",
            Mapping = new Dictionary<string, OpenApiSchemaReference>
            {
                ["Add"] = new(
                    referenceId: nameof(CashVoucherBulkAddItemRequest),
                    hostDocument: null!,
                    externalResource: null),
                ["Update"] = new(
                    referenceId: nameof(CashVoucherBulkUpdateItemRequest),
                    hostDocument: null!,
                    externalResource: null),
                ["Delete"] = new(
                    referenceId: nameof(CashVoucherBulkDeleteItemRequest),
                    hostDocument: null!,
                    externalResource: null)
            }
        };
        openApiSchema.Properties ??=
            new Dictionary<string, IOpenApiSchema>();
        openApiSchema.Properties["action"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum =
            [
                JsonValue.Create("Add"),
                JsonValue.Create("Update"),
                JsonValue.Create("Delete")
            ]
        };
        openApiSchema.Required ??= new HashSet<string>();
        openApiSchema.Required.Add("action");
    }
}
