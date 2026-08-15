using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StockTransfersSwaggerDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(StockTransfersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StockTransfersController.GetAll) => (
                "Get stock transfers",
                SwaggerOperationDescription.Create(
                    "Returns one header-only page of current-company stock transfers ordered by transfer date and ID descending.",
                    "Supports search by document or store name, sourceStoreId, destinationStoreId, itemId, and inclusive fromDate/toDate filters.",
                    "Only active records are returned. Open GET /StockTransfers/{id} for item lines and source/destination costing effects.",
                    "Invalid pagination or filters return 400.")),
            nameof(StockTransfersController.GetById) => (
                "Get a stock transfer",
                SwaggerOperationDescription.Create(
                    "Returns the complete transfer and each paired TransferOut/TransferIn movement, including unit cost, total cost, resulting quantity, average cost, and inventory value in both stores.",
                    "Requires an authenticated current-company context and a positive route ID.",
                    "The source outbound average cost is the destination inbound unit cost.",
                    "Missing, deleted, and other-company transfers return 404.")),
            nameof(StockTransfersController.Create) => (
                "Create a stock transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Atomically creates one TransferOut and one TransferIn movement per line and recalculates both stores. The source movement uses the chronological weighted-average cost; exactly that cost enters the destination and changes its weighted average.",
                    "Requires a unique documentNumber, transferDate, different active company-owned product source/destination stores, optional notes, and 1-100 unique active items with positive quantities. Item units are server-derived.",
                    "The complete historical source timeline must remain non-negative. The client cannot send unit cost or company ID.",
                    "Missing references return 404; duplicate numbers, inactive references, container stores, or insufficient stock return 409.")),
            nameof(StockTransfersController.Update) => (
                "Update a stock transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces transfer date, notes, and the complete line set, preserves the document number and both stores, synchronizes paired movements, and replays affected costing timelines.",
                    "Requires the current 8-byte rowVersion. Send the complete desired line collection.",
                    "A reduced or moved destination inbound quantity is rejected if it would make a later destination balance negative.",
                    "Stale RowVersion, stock timeline conflicts, and costing conflicts return 409.")),
            nameof(StockTransfersController.Delete) => (
                "Delete a stock transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes the transfer, lines, and paired movements, then recalculates both stores and all downstream transfer costs atomically.",
                    "Requires a positive route ID.",
                    "Deletion is rejected when removing the destination receipt would make later destination stock negative.",
                    "Missing records return 404; stock conflicts return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"StockTransfers_{context.MethodInfo.Name}";
    }
}
