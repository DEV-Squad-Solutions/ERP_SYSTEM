using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CountriesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CountriesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CountriesController.GetAll) => (
                "Get paginated countries",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of global, non-deleted countries, ordered by name and ID. The list includes active and inactive records.",
                    "An authenticated access token. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Countries are global and are not filtered by the selected company.")),
            nameof(CountriesController.GetSelect) => (
                "Get active countries for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted global countries as ID and name pairs.",
                    "An authenticated access token.",
                    "No request fields.",
                    "Returns an empty array when no active country exists. Inactive and soft-deleted countries are excluded.")),
            nameof(CountriesController.GetById) => (
                "Get a country",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted global country.",
                    "An authenticated access token and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 (`Countries.InvalidId`). Missing and soft-deleted countries return 404 (`Countries.NotFound`).")),
            nameof(CountriesController.Create) => (
                "Create a country",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates global country reference data.",
                    "`code`, `name`, and `arabicName`; `isActive` defaults to true.",
                    "Strings are trimmed and must be non-empty. Maximum lengths: code 50, name 200, Arabic name 200.",
                    "An active country code must be globally unique; normal duplicates return 409 (`Countries.CodeExists`). Multiple inactive records may share a code. If simultaneous writes pass the pre-check, the database rejects one and the current global handler returns 500; reload before retrying.")),
            nameof(CountriesController.Update) => (
                "Update a country",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a global country while preserving identity and creation audit fields.",
                    "A positive route `id` and the complete country request.",
                    "All create validation applies; active-code duplicate checks exclude the current country.",
                    "Invalid IDs return 400 (`Countries.InvalidId`); missing or deleted countries return 404 (`Countries.NotFound`). Activating a duplicate active code returns 409 (`Countries.CodeExists`); a simultaneous unique-index race currently returns 500.")),
            nameof(CountriesController.Delete) => (
                "Delete a country",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a country; audit history remains.",
                    "A positive route `id` and an Admin access token.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 (`Countries.InvalidId`). Missing, already-deleted, and repeated deletes return 404 (`Countries.NotFound`).")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Countries_{context.MethodInfo.Name}";
    }
}
