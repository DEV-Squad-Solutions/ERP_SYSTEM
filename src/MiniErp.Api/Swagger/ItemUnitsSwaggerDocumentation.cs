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
                "Get all item units",
                "Returns the complete list of non-deleted item units."),
            nameof(ItemUnitsController.GetSelect) => (
                "Get item units for selection",
                "Returns active item units as ID and name pairs for dropdown controls."),
            nameof(ItemUnitsController.GetById) => (
                "Get an item unit",
                "Returns one non-deleted item unit by its integer ID."),
            nameof(ItemUnitsController.Create) => (
                "Create an item unit",
                "Creates an item unit with a unique name."),
            nameof(ItemUnitsController.Update) => (
                "Update an item unit",
                "Updates an existing item unit while preserving its ID and creation audit information."),
            nameof(ItemUnitsController.Delete) => (
                "Delete an item unit",
                "Soft-deletes an unused item unit. Units assigned to items cannot be deleted."),
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
