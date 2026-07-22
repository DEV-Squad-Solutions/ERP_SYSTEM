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
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of non-deleted item units for the selected company with total-count metadata.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Other-company and soft-deleted units are excluded.")),
            nameof(ItemUnitsController.GetSelect) => (
                "Get item units for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted item units for the selected company as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Inactive, deleted, and other-company units are excluded.")),
            nameof(ItemUnitsController.GetById) => (
                "Get an item unit",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted item unit owned by the selected company.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company units return 404.")),
            nameof(ItemUnitsController.Create) => (
                "Create an item unit",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates an item unit in the selected company.",
                    "`name`; `isActive` defaults to true.",
                    "Name is trimmed, must be non-empty, and cannot exceed 100 characters.",
                    "The normalized name must be unique within the company or 409 is returned. `CompanyId` always comes from the token.")),
            nameof(ItemUnitsController.Update) => (
                "Update an item unit",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates an item unit while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id`, `name`, and optional `isActive`.",
                    "Name follows the create rules; duplicate checks exclude the current unit.",
                    "Invalid IDs return 400; missing, deleted, and other-company units return 404; normalized duplicate names return 409.")),
            nameof(ItemUnitsController.Delete) => (
                "Delete an item unit",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unused item unit in the selected company.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400; missing, deleted, and other-company units return 404. References from current or historical items block deletion with 409.")),
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
