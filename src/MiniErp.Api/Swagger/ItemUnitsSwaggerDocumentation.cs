using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ItemUnitsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ItemUnitsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ItemUnitsController.GetAll) => (
                "Get paginated item units",
                "Returns one page of non-deleted item units with total-count metadata. Page size is limited to 100."),
            nameof(ItemUnitsController.GetSelect) => (
                "Get item units for selection",
                "Returns active item units as ID and name pairs for dropdown controls."),
            nameof(ItemUnitsController.GetById) => (
                "Get an item unit",
                "Returns one non-deleted item unit by its integer ID."),
            nameof(ItemUnitsController.Create) => (
                "Create an item unit",
                "Admin only. Creates an item unit with a unique name."),
            nameof(ItemUnitsController.Update) => (
                "Update an item unit",
                "Admin only. Updates an item unit while preserving its ID and creation audit information."),
            nameof(ItemUnitsController.Delete) => (
                "Delete an item unit",
                "Admin only. Soft-deletes an unused item unit. Units referenced by current or historical items cannot be deleted."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"ItemUnits_{context.MethodInfo.Name}";
    }
}
