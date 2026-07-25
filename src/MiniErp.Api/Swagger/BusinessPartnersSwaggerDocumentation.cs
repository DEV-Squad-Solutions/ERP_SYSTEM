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
                SwaggerOperationDescription.Create(
                    "Returns a deterministic page of shared customer/supplier records for the selected company, ordered by name and ID. Each item also includes its active container Store, when present, and the complete active Containers workspace with `isAssigned` and `storeContainerId`.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. Pages beyond the result set are empty. Records from other companies and soft-deleted records are never returned. Partners without an active container Store return `containerStore: null` and an empty `containers` array.")),
            nameof(BusinessPartnersController.GetSelect) => (
                "Get active business partners for selection",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted business partners for the selected company as ID and name pairs.",
                    "A bearer token containing one `company_id`.",
                    "No request fields.",
                    "Returns an empty array when none are available. Inactive, deleted, and other-company records are excluded.")),
            nameof(BusinessPartnersController.GetById) => (
                "Get a business partner",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted business partner owned by the selected company.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company records return 404 without revealing tenant data.")),
            nameof(BusinessPartnersController.GetContainerStore) => (
                "Get a business partner with its container store",
                SwaggerOperationDescription.Create(
                    "Returns one company-owned BusinessPartner together with its active container Store and one active Containers list. Each Container includes `isAssigned` and `storeContainerId`, allowing an edit screen to render the complete relationship without repeated nested BusinessPartner data.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company BusinessPartners return 404. `containerStore` is null when the partner has no active container Store; `storeContainers` is then empty. Use `PUT /BusinessPartners/{id}`, `PUT /Stores/{id}`, `PUT /Containers/{id}`, and `PUT /StoreContainers/upsert` for edits.")),
            nameof(BusinessPartnersController.Create) => (
                "Create a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a shared customer/supplier record in the selected company.",
                    "`code`, `name`, `currency`, and `creditLimit`. Phone, email, address, and tax number are optional; `isActive` defaults to true.",
                    "Maximum lengths: code 50, name 200, phone 50, email 256, address 500, taxNumber 100. Email must be valid when supplied; currency must be defined; creditLimit must be non-negative with at most 18 digits and 2 decimals.",
                    "Normalized name and code must be unique per company; a supplied tax number must also be unique. Duplicates return 409. `CompanyId` always comes from the token.")),
            nameof(BusinessPartnersController.Update) => (
                "Update a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a business partner in the selected company while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same request fields required by create.",
                    "All create validation rules apply; duplicate checks exclude the current record.",
                    "Invalid IDs return 400; missing, deleted, and other-company records return 404; normalized name, code, or tax-number conflicts return 409.")),
            nameof(BusinessPartnersController.Delete) => (
                "Delete a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a business partner in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company records return 404. A partner linked to any current or historical container store returns 409. A repeated delete is not treated as success.")),
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
