using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StockOpeningBalancesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(StockOpeningBalancesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StockOpeningBalancesController.GetAll) => (
                "Get paginated stock opening balances",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of stock opening balances owned by the selected company. Supplied filters are combined with AND. Every item includes its complete ordered line details, row-version token, and server-calculated movement unit cost, average cost, and inventory value snapshots.",
                    "A bearer token containing one validated `company_id`; optional query fields are `pageNumber`, `pageSize`, `documentNumber`, `storeId`, `fromDate`, and `toDate`.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100; `storeId` must be positive; and `toDate` must not precede `fromDate`.",
                    "Invalid pagination returns 400. Empty and later pages return an empty `items` array. Deleted and other-company records are excluded.")),
            nameof(StockOpeningBalancesController.GetById) => (
                "Get a stock opening balance",
                SwaggerOperationDescription.Create(
                    "Returns one stock opening balance header and its item lines, including the nullable server-derived item unit, calculated quantity and total, movement cost status, unit cost, average cost, inventory value, and row-version token.",
                    "A bearer token containing one validated `company_id` and a positive route `id`.",
                    "No company ID is accepted from the client. Quantity and total are calculated by the server.",
                    "Invalid IDs return 400. Missing, deleted, and other-company records return 404.")),
            nameof(StockOpeningBalancesController.Create) => (
                "Create a stock opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a header, lines, stable opening-balance movements, weighted-average snapshots, and current item/store balances in one Serializable transaction. Audit fields are populated by the shared audit interceptor.",
                    "`storeId`, `documentNumber`, `documentDate`, and a non-empty `lines` array. Each line contains `itemId`, `count`, `weight`, `price`, and optional `notes`; do not send `companyId`, `itemUnitId`, `quantity`, or `total`.",
                    "The store must be an active product store in the selected company. Document numbers are trimmed, limited to 50 characters, and unique among non-deleted records. At most 100 unique item lines are allowed. Count and weight must be greater than zero; price may be zero but cannot be negative. The server calculates quantity as count * weight and rounds total = quantity * price to two decimal places.",
                    "Missing or other-company stores/items return 404. Inactive items, inactive item units, container stores, duplicate document numbers, and invalid values return the documented 400/409 business errors.")),
            nameof(StockOpeningBalancesController.Update) => (
                "Update a stock opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces the header and complete lines, preserves matching movement identities, validates the resulting stock timeline, and replays all affected costing timelines atomically.",
                    "Positive route `id`, the same fields as create, and the current `rowVersion` returned by the API.",
                    "The complete line set is required; item units, quantities, and totals are re-derived by the server. Document number uniqueness is checked per company and excludes the current record. Every successful update advances the header row version, including line-only changes.",
                    "Missing or other-company records return 404. A stale row version returns 409 (`StockOpeningBalances.Concurrency`). This contract has no document-status, posting, cancellation, or movement operation.")),
            nameof(StockOpeningBalancesController.Delete) => (
                "Delete a stock opening balance",
                SwaggerOperationDescription.Create(
                    "Admin only. Validates the resulting timeline, soft-deletes the header, lines, and opening movements, and recalculates all affected cost snapshots and balances atomically.",
                    "A positive route `id` and an Admin bearer token containing one validated `company_id`.",
                    "No request body is required.",
                    "Missing, deleted, and other-company records return 404. A configured stock policy may reject deletion when later outbound movements would become invalid.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"StockOpeningBalances_{context.MethodInfo.Name}";
    }
}
