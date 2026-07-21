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
                "Returns a deterministic page of non-deleted drivers for the active company, ordered by name and ID. Page size is limited to 100."),
            nameof(DriversController.GetSelect) => (
                "Get available drivers for selection",
                "Returns active, non-deleted drivers for the active company whose licence has not expired, as ID and name pairs."),
            nameof(DriversController.GetById) => (
                "Get a driver",
                "Returns one non-deleted driver owned by the active company."),
            nameof(DriversController.Create) => (
                "Create a driver",
                "Admin only. Creates a driver in the active company. Code and licence number are required and unique within that company; a supplied national ID must also be unique."),
            nameof(DriversController.Update) => (
                "Update a driver",
                "Admin only. Updates a driver in the active company while preserving its ID, CompanyId, and creation audit information."),
            nameof(DriversController.Delete) => (
                "Delete a driver",
                "Admin only. Deactivates and soft-deletes a driver in the active company. The record remains available for auditing."),
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
