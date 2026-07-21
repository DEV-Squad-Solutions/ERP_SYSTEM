using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class BusinessPartnersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(BusinessPartnersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(BusinessPartnersController.GetAll) => (
                "Get paginated business partners",
                "Returns a deterministic page of shared customer/supplier records for the active company, ordered by name and ID. Page size is limited to 100."),
            nameof(BusinessPartnersController.GetSelect) => (
                "Get active business partners for selection",
                "Returns active, non-deleted business partners for the active company as ID and name pairs."),
            nameof(BusinessPartnersController.GetById) => (
                "Get a business partner",
                "Returns one non-deleted business partner owned by the active company."),
            nameof(BusinessPartnersController.Create) => (
                "Create a business partner",
                "Admin only. Creates a shared customer/supplier record in the active company. Code and any supplied tax number must be unique within that company."),
            nameof(BusinessPartnersController.Update) => (
                "Update a business partner",
                "Admin only. Updates a business partner in the active company while preserving its ID, CompanyId, and creation audit information."),
            nameof(BusinessPartnersController.Delete) => (
                "Delete a business partner",
                "Admin only. Deactivates and soft-deletes a business partner. The record remains available for auditing."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"BusinessPartners_{context.MethodInfo.Name}";
    }
}
