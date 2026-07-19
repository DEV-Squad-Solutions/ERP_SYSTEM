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
                "Get paginated items",
                "Returns one page of non-deleted items with unit details and total-count metadata. Page size is limited to 100."),
            nameof(ItemsController.GetSelect) => (
                "Get items for selection",
                "Returns active items with active item units as ID and name pairs for dropdown controls."),
            nameof(ItemsController.GetById) => (
                "Get an item",
                "Returns one non-deleted item by its integer ID."),
            nameof(ItemsController.Create) => (
                "Create an item",
                "Admin only. Creates an item after validating its unique code and active item unit."),
            nameof(ItemsController.Update) => (
                "Update an item",
                "Admin only. Updates an item with an active item unit while preserving its ID and creation audit information."),
            nameof(ItemsController.Delete) => (
                "Delete an item",
                "Admin only. Soft-deletes an item. The record remains in the database for auditing."),
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
