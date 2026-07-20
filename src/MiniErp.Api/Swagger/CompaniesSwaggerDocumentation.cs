using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CompaniesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CompaniesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CompaniesController.GetAll) => (
                "Get all companies",
                "Admin only. Returns a deterministic page of non-deleted companies ordered by name and ID. " +
                "Use the pageNumber and pageSize query parameters; pageSize must be between 1 and 100."),
            nameof(CompaniesController.GetSelect) => (
                "Get companies for selection",
                "Admin only. Returns non-deleted companies as ID and name pairs for administrative controls. Assigned companies are also returned by login for normal company selection."),
            nameof(CompaniesController.GetById) => (
                "Get a company",
                "Admin only. Returns one non-deleted company by its integer ID."),
            nameof(CompaniesController.Create) => (
                "Create a company",
                "Admin only. Creates a company with unique commercial-register and tax-number values."),
            nameof(CompaniesController.Update) => (
                "Update a company",
                "Admin only. Updates a company while preserving its ID and creation audit information."),
            nameof(CompaniesController.Delete) => (
                "Delete a company",
                "Admin only. Soft-deletes an unused company. Companies with user assignments or current/historical business data return 409 Conflict."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Companies_{context.MethodInfo.Name}";
    }
}
