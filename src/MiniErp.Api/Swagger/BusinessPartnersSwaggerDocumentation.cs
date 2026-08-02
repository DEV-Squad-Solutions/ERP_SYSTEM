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
                    "Returns a deterministic page of shared customer/supplier records for the selected company, ordered by name and ID. Supplied filters are combined with AND. Each item also includes its active container Store and active assigned Containers.",
                    "A bearer token containing one `company_id`. Optional query fields are `pageNumber`, `pageSize`, `search`, `code`, `name`, `taxNumber`, `currency`, and `isActive`.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100; and enum values must be supported.",
                    "Invalid pagination returns 400. Pages beyond the result set are empty. Records from other companies and soft-deleted records are never returned. Partners without an active container Store or active assignments return an empty `containers` array.")),
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
                    "Returns one non-deleted business partner owned by the selected company together with its active container Store, when present, all active Containers, and any inactive Container still assigned to that Store. Each Container includes `isActive`, `isAssigned`, and `storeContainerId`, so inactive assignments remain visible and removable.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company records return 404 without revealing tenant data. Partners without an active container Store return `containerStore: null` and an empty `containers` array.")),
            nameof(BusinessPartnersController.GetContainerStore) => (
                "Get a business partner container store",
                SwaggerOperationDescription.Create(
                    "Returns only the active container Store linked to one business partner and the active Containers assigned to that Store. It does not return the full business-partner detail or unassigned company Containers. Each returned Container includes `isAssigned: true` and its `storeContainerId`.",
                    "A bearer token containing one `company_id` and route `id` for the business partner.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, deleted, and other-company partners return 404 without revealing tenant data. A partner without an active container Store also returns 404.")),
            nameof(BusinessPartnersController.GetItemReport) => (
                "Get item movements for a business partner",
                SwaggerOperationDescription.Create(
                    "Returns sales and purchase invoice movements with optional business-partner and item filters. The report is company-scoped and read-only.",
                    "A bearer token containing one `company_id`. Optional query fields are `businessPartnerId`, `itemId`, `countryId`, `search`, `movementType`, `fromDate`, and `toDate`.",
                    "When `businessPartnerId` is supplied, it must be positive and company-owned; when omitted, all business partners are included. When `itemId` is omitted, all items are included; when supplied, it must be positive. `movementType` accepts `Sales` or `Purchase`; and the start date cannot be after the end date. Search matches invoice number, partner invoice number, or notes. Every movement returns the persisted invoice-line `count` together with quantity and weight.",
                    "Returns `quantity` from the invoice-line count, `weight` as count multiplied by unit weight, and line `unitPrice` and `totalAmount`. Sales and purchase returns are not included.")),
            nameof(BusinessPartnersController.Create) => (
                "Create a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a shared customer/supplier record in the selected company.",
                    "`code`, `name`, `currency`, and `creditLimit`. Phone, email, address, and tax number are optional; `isActive` defaults to true.",
                    "Maximum lengths: code 50, name 200, phone 50, email 256, address 500, taxNumber 100. Email must be valid when supplied; currency must be defined; creditLimit must be non-negative with at most 18 digits and 2 decimals.",
                    "Normalized name and code must be unique per company without case sensitivity; a supplied tax number follows the same rule. Duplicates return 409. `CompanyId` always comes from the token.")),
            nameof(BusinessPartnersController.Update) => (
                "Update a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a business partner in the selected company while preserving identity, tenant, and creation audit fields.",
                    "A positive route `id` and the same request fields required by create.",
                    "All create validation rules apply; duplicate checks exclude the current record. Currency may change only before the partner has any current or historical financial record.",
                    "Invalid IDs return 400; missing, deleted, and other-company records return 404; normalized name, code, or tax-number conflicts return 409. A protected currency change returns 409 (`BusinessPartners.CurrencyChangeNotAllowed`).")),
            nameof(BusinessPartnersController.Delete) => (
                "Delete a business partner",
                SwaggerOperationDescription.Create(
                    "Admin only. Deactivates and soft-deletes a business partner in the selected company; audit history remains.",
                    "A positive route `id` and an Admin bearer token containing one `company_id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400. Missing, already-deleted, and other-company records return 404. Current or historical container stores return 409 (`BusinessPartners.HasContainerStores`); invoices, partner opening balances, movements, or driver trips return 409 (`BusinessPartners.HasFinancialRecords`). A repeated delete is not treated as success.")),
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
