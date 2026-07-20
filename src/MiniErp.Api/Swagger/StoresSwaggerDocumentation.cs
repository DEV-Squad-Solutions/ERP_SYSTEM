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
                "Returns a deterministic page of non-deleted stores for the active company, ordered by name and ID. " +
                "Use pageNumber and pageSize; pageSize must be between 1 and 100."),
            nameof(StoresController.GetSelect) => (
                "Get active stores for selection",
                "Returns active, non-deleted stores for the active company as ID and name pairs for dropdown controls."),
            nameof(StoresController.GetById) => (
                "Get a store",
                "Returns one non-deleted store owned by the active company."),
            nameof(StoresController.Create) => (
                "Create a store",
                "Admin only. Creates a store in the active company. The normalized code must be unique within that company."),
            nameof(StoresController.Update) => (
                "Update a store",
                "Admin only. Updates a store in the active company while preserving its ID and creation audit information."),
            nameof(StoresController.Delete) => (
                "Delete a store",
                "Soft-deletes and deactivates a store. The record remains available for auditing."),
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
