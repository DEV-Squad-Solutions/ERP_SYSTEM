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
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of non-deleted items for the selected company, including unit details and total-count metadata.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Other-company and soft-deleted items are excluded.")),
            nameof(ItemsController.GetSelect) => (
                "Get items for selection",
                SwaggerOperationDescription.Create(
                    "Returns active items in the selected company whose item unit is also active, as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Inactive or deleted items, items with inactive units, and other-company records are excluded.")),
            nameof(ItemsController.GetById) => (
                "Get an item",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted item owned by the selected company, including its unit details.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company items return 404.")),
            nameof(ItemsController.Create) => (
                "Create an item",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates an item in the selected company.",
                    "`itemUnitId`, `code`, and `name`. Description is optional; `isActive` defaults to true.",
                    "`itemUnitId` must be greater than zero. Maximum lengths: code 50, name 200, description 1000. Required strings are trimmed and non-empty.",
                    "The normalized code must be unique per company or 409 is returned. The item unit must exist, be active, and belong to the selected company; unavailable units return 404 or 409 according to state.")),
            nameof(ItemsController.Update) => (
                "Update an item",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates an item while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same request fields required by create.",
                    "All create validation rules apply; duplicate-code checks exclude the current item.",
                    "Invalid IDs return 400; missing, deleted, or other-company items return 404; duplicate codes return 409. The selected unit must remain active and in the same company.")),
            nameof(ItemsController.Delete) => (
                "Delete an item",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes an item in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company items return 404. A repeated delete is not treated as success.")),
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
