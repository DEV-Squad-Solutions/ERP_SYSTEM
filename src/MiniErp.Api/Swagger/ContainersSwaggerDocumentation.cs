using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ContainersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ContainersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ContainersController.GetAll) => (
                "Get paginated containers",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of non-deleted reusable container types for the selected company, ordered by name and ID. The list includes active and inactive records.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Other-company and soft-deleted containers are excluded.")),
            nameof(ContainersController.GetSelect) => (
                "Get active containers for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted container types for the selected company as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Inactive, deleted, and other-company containers are excluded.")),
            nameof(ContainersController.GetById) => (
                "Get a container",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted container type owned by the selected company.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 (`Containers.InvalidId`). Missing, deleted, and other-company containers return 404 (`Containers.NotFound`).")),
            nameof(ContainersController.Create) => (
                "Create a container",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a reusable container type in the selected company.",
                    "`code` and `name`; `description` is optional and `isActive` defaults to true.",
                    "Required strings are trimmed and non-empty. Maximum lengths: code 50, name 200, description 1000. Blank descriptions become null.",
                    "An active code must be unique within the selected company; normal duplicates return 409 (`Containers.CodeExists`). Inactive duplicates are allowed and `CompanyId` comes only from the token. If simultaneous writes pass the pre-check, the database rejects one and the current global handler returns 500; reload before retrying.")),
            nameof(ContainersController.Update) => (
                "Update a container",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a container type while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the complete container request.",
                    "All create validation applies; active-code duplicate checks exclude the current container.",
                    "Invalid IDs return 400 (`Containers.InvalidId`); missing, deleted, and other-company containers return 404 (`Containers.NotFound`). Activating a duplicate active code returns 409 (`Containers.CodeExists`); a simultaneous unique-index race currently returns 500.")),
            nameof(ContainersController.Delete) => (
                "Delete a container",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes an unused container type in the selected company.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 (`Containers.InvalidId`); missing, deleted, and other-company containers return 404 (`Containers.NotFound`). Current or historical store assignments block deletion with 409 (`Containers.HasStoreAssignments`).")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Containers_{context.MethodInfo.Name}";
    }
}
