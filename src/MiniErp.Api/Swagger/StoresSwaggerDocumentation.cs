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
                    "Returns a deterministic page of non-deleted product and customer container stores for the selected company, ordered by name and ID. Container stores include their linked business-partner ID and name.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Other-company and soft-deleted stores are excluded.")),
            nameof(StoresController.GetSelect) => (
                "Get active stores for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted product stores for the selected company as ID and name pairs. Use this selector for product inventory and invoices.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Container, inactive, deleted, and other-company stores are excluded.")),
            nameof(StoresController.GetContainerSelect) => (
                "Get active container stores for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted container stores linked to active business partners in the selected company as ID and name pairs. Use this selector when managing store-container assignments.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are usable. Product stores, inactive stores, stores with inactive partners, deleted stores, and other-company stores are excluded.")),
            nameof(StoresController.GetById) => (
                "Get a store",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted store owned by the selected company, including its type and linked business-partner details when it is a container store.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company stores return 404.")),
            nameof(StoresController.Create) => (
                "Create a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates either a product store or a business-partner-specific container store in the selected company.",
                    "`code`, `name`, and `isContainerStore`. `businessPartnerId` is required only when `isContainerStore` is true. Address is optional; `isActive` defaults to true.",
                    "Required strings are trimmed and non-empty. Maximum lengths: code 50, name 200, address 500. A container store requires a positive, active business partner from the selected company; a product store requires `businessPartnerId` to be null.",
                    "The normalized code must be unique within the company. A business partner can have at most one active container store. Duplicates detected before saving return 409; database unique indexes provide final concurrency protection. A missing or other-company partner returns 404; an inactive partner returns 409. Duplicate names are allowed. `CompanyId` always comes from the token.")),
            nameof(StoresController.Update) => (
                "Update a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a store in the selected company while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same conditional request fields required by create.",
                    "All create validation and business-partner rules apply; duplicate-code checks exclude the current store. Changing between product and container types must also add or remove `businessPartnerId` accordingly. A store with current or historical container assignments cannot change its type or linked business partner.",
                    "Invalid IDs return 400; missing stores or partners return 404. Duplicate codes, inactive partners, second active container-store assignments, and protected assignment identity changes return 409; database unique indexes provide final concurrency protection.")),
            nameof(StoresController.Delete) => (
                "Delete a store",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a store in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company stores return 404. Current or historical StoreContainer assignments block deletion with 409 (`Stores.HasContainerAssignments`). A repeated delete is not treated as success.")),
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
