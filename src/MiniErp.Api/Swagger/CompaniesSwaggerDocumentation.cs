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
                SwaggerOperationDescription.Create(
                    "Admin only. Returns a deterministic page of non-deleted companies ordered by name and ID, including each company's stock-balance check mode and base currency. Supplied filters are combined with AND.",
                    "An Admin bearer token. Optional query fields are `pageNumber`, `pageSize`, `search`, `name`, `address`, `commercialRegister`, `taxNumber`, and `managerName`.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the available data returns an empty item list with valid pagination metadata.")),
            nameof(CompaniesController.GetSelect) => (
                "Get companies for selection",
                SwaggerOperationDescription.Create(
                    "Admin only. Returns non-deleted companies as ID and name pairs for administrative controls. Assigned companies are returned separately by login for normal session selection.",
                    "An Admin bearer token.",
                    "No request fields.",
                    "Returns an empty array when no companies are available. Deleted companies are excluded.")),
            nameof(CompaniesController.GetById) => (
                "Get a company",
                SwaggerOperationDescription.Create(
                    "Admin only. Returns one non-deleted company by its integer ID, including its stock-balance check mode and base currency.",
                    "An Admin bearer token and route `id`.",
                    "`id` must be greater than zero.",
                    "Zero or negative IDs return 400. Missing or soft-deleted companies return 404.")),
            nameof(CompaniesController.Create) => (
                "Create a company",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a company and grants the authenticated admin access to it atomically.",
                    "`name`, `address`, `commercialRegister`, `taxNumber`, and `managerName`. Optional `stockBalanceCheckMode` is `None`, `DateCheck`, `FinalCheck`, or `Both`; optional `baseCurrency` defaults to `EGP`.",
                    "Required strings are trimmed and cannot be empty. Maximum lengths: name and managerName 200; address 500; commercialRegister and taxNumber 50.",
                    "Duplicate commercial-register or tax-number values return 409. The entire create-and-assign operation rolls back on failure. The admin must log in again before selecting the new company.")),
            nameof(CompaniesController.Update) => (
                "Update a company",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a company while preserving its ID and creation audit information.",
                    "An Admin bearer token, positive route `id`, and all company request fields. `stockBalanceCheckMode` may be changed independently; `baseCurrency` is locked after financial or inventory history exists.",
                    "The same requiredness and maximum lengths as create apply. Duplicate checks exclude the current company.",
                    "Invalid IDs return 400; missing or deleted companies return 404; commercial-register or tax-number conflicts return 409.")),
            nameof(CompaniesController.Delete) => (
                "Delete a company",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unused company.",
                    "An Admin bearer token and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 and missing or already-deleted companies return 404. User assignments or current/historical business data block deletion with 409.")),
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
