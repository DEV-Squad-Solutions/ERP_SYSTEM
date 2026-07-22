using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StoresSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(StoresController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StoresController.GetAll) => (
                "Get stores",
                SwaggerOperationDescription.Create(
                    "Returns a deterministic page of non-deleted stores for the selected company, ordered by name and ID.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Other-company and soft-deleted stores are excluded.")),
            nameof(StoresController.GetSelect) => (
                "Get active stores for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted stores for the selected company as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Inactive, deleted, and other-company stores are excluded.")),
            nameof(StoresController.GetById) => (
                "Get a store",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted store owned by the selected company.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company stores return 404.")),
            nameof(StoresController.Create) => (
                "Create a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a store in the selected company.",
                    "`code` and `name`. Address is optional; `isActive` defaults to true.",
                    "Required strings are trimmed and non-empty. Maximum lengths: code 50, name 200, address 500.",
                    "The normalized code must be unique within the company or 409 is returned. Duplicate names are allowed. `CompanyId` always comes from the token.")),
            nameof(StoresController.Update) => (
                "Update a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a store in the selected company while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same request fields required by create.",
                    "All create validation rules apply; duplicate-code checks exclude the current store.",
                    "Invalid IDs return 400; missing, deleted, and other-company stores return 404; normalized duplicate codes return 409.")),
            nameof(StoresController.Delete) => (
                "Delete a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a store in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company stores return 404. A repeated delete is not treated as success.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Stores_{context.MethodInfo.Name}";
    }
}
