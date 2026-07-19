using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ItemsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ItemsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ItemsController.GetAll) => (
                "Get all items",
                "Returns the complete list of non-deleted items with their unit details."),
            nameof(ItemsController.GetSelect) => (
                "Get items for selection",
                "Returns active items as ID and name pairs for dropdown controls."),
            nameof(ItemsController.GetById) => (
                "Get an item",
                "Returns one non-deleted item by its integer ID."),
            nameof(ItemsController.Create) => (
                "Create an item",
                "Creates an item after validating its code and item unit."),
            nameof(ItemsController.Update) => (
                "Update an item",
                "Updates an existing item while preserving its ID and creation audit information."),
            nameof(ItemsController.Delete) => (
                "Delete an item",
                "Soft-deletes an item. The record remains in the database for auditing."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Items_{context.MethodInfo.Name}";
    }
}
