using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class DriversSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(DriversController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(DriversController.GetAll) => (
                "Get paginated drivers",
                SwaggerOperationDescription.Create(
                    "Returns a deterministic page of non-deleted drivers for the selected company, ordered by name and ID.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Expired and inactive drivers remain visible in this administrative list.")),
            nameof(DriversController.GetSelect) => (
                "Get available drivers for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted drivers for the selected company whose licence has not expired, as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields. A null expiry date is treated as not expired.",
                    "Returns an empty array when no driver is available. Inactive, deleted, expired, and other-company drivers are excluded.")),
            nameof(DriversController.GetById) => (
                "Get a driver",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted driver owned by the selected company.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company drivers return 404.")),
            nameof(DriversController.Create) => (
                "Create a driver",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a driver in the selected company.",
                    "`code`, `name`, and `licenseNumber`. Phone, national ID, and licence expiry date are optional; `isActive` defaults to true.",
                    "Required values are trimmed and non-empty. Maximum lengths: code 50, name 200, phone 50, nationalId 50, licenseNumber 100.",
                    "Normalized name, code, and licence number must be unique per company; a supplied national ID must also be unique. Duplicates return 409. A past expiry date is accepted but excludes the driver from `/select`.")),
            nameof(DriversController.Update) => (
                "Update a driver",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a driver in the selected company while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same request fields required by create.",
                    "All create validation rules apply; duplicate checks exclude the current driver.",
                    "Invalid IDs return 400; missing, deleted, and other-company drivers return 404; normalized unique-value conflicts return 409.")),
            nameof(DriversController.Delete) => (
                "Delete a driver",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a driver in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company drivers return 404. A repeated delete is not treated as success.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Drivers_{context.MethodInfo.Name}";
    }
}
