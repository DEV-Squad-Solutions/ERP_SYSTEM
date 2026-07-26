using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class PartnerOpeningBalancesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(PartnerOpeningBalancesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(PartnerOpeningBalancesController.GetAll) => (
                "Get paginated partner opening balances",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of partner opening balances owned by the selected company. Every item is the complete detail response and includes the business partner ID and name, document fields, balance type, currency, amount, notes, and row-version token.",
                    "A bearer token containing one validated `company_id`; `pageNumber` and `pageSize` are optional.",
                    "`pageNumber` must be greater than zero and `pageSize` must be between 1 and 100. Currency and balance type are returned as enum names.",
                    "Invalid pagination returns 400. Empty and later pages return an empty `items` array. Deleted and other-company records are excluded; this endpoint never returns a reduced header-only item.")),
            nameof(PartnerOpeningBalancesController.GetById) => (
                "Get a partner opening balance",
                SwaggerOperationDescription.Create(
                    "Returns one complete partner opening balance detail response for the selected company.",
                    "A bearer token containing one validated `company_id` and a positive route `id`.",
                    "The response includes the active partner ID and name, document number and date, receivable/payable type, currency, positive amount, optional notes, and row-version token. No company ID is accepted from the client.",
                    "Invalid IDs return 400. Missing, deleted, and other-company records return 404.")),
            nameof(PartnerOpeningBalancesController.Create) => (
                "Create a partner opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates one receivable or payable partner opening balance atomically for the selected company. Audit fields are populated only by the shared audit interceptor.",
                    "`businessPartnerId`, `documentNumber`, `documentDate`, `currency`, `balanceType`, and `amount`; `notes` is optional. Do not send `companyId` or `rowVersion`.",
                    "The partner must be active and belong to the selected company. The supplied currency must match the partner currency. Document numbers are trimmed, limited to 50 characters, and unique among non-deleted records. Amount must be positive and have at most two decimal places. Notes are trimmed, blank notes become null, and the normalized value is limited to 1,000 characters. Currency values are `EGP`, `USD`, `EUR`, `GBP`, `SAR`, `AED`, or `KWD`; balance types are `Receivable` or `Payable`.",
                    "Invalid values return 400. Missing or other-company partners return 404. Inactive partners, currency mismatches, and duplicate document numbers return 409.")),
            nameof(PartnerOpeningBalancesController.Update) => (
                "Update a partner opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates the complete header using row-version concurrency. The audit interceptor records the update and the header row-version advances when a value changes.",
                    "Positive route `id`, the same fields as create, and the current `rowVersion` returned by the API. The request must not contain `companyId`.",
                    "The active partner and matching currency are revalidated. The document number is normalized and checked for company-scoped uniqueness, excluding the current record. The client row-version is used as EF Core's original concurrency value and is never replaced with a freshly loaded token.",
                    "Missing or other-company records return 404. A missing token returns 400. A stale token returns 409 with `PartnerOpeningBalances.Concurrency` and a reload-and-retry message. This contract has no status, posting, cancellation, reversal, or partner-movement operation.")),
            nameof(PartnerOpeningBalancesController.Delete) => (
                "Delete a partner opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes one partner opening balance; audit history remains available through query-filter bypasses.",
                    "A positive route `id` and an Admin bearer token containing one validated `company_id`. No request body is required.",
                    "The operation does not create reversal or partner-movement records.",
                    "Missing, deleted, and other-company records return 404. A concurrent database update returns 409 with a reload-and-retry conflict.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"PartnerOpeningBalances_{context.MethodInfo.Name}";
    }
}
