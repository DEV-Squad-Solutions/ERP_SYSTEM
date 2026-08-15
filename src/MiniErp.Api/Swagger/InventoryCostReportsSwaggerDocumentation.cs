using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class InventoryCostReportsSwaggerDocumentation
    : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(InventoryReportsController) ||
            context.MethodInfo.Name != nameof(InventoryReportsController.GetCostReport))
        {
            return;
        }

        operation.Summary = "Get the weighted-average inventory cost report";
        operation.Description =
            "Returns the deterministic Company + Store + Item costing cycle. " +
            "Each movement includes inbound/outbound quantity, cost status, " +
            "pending quantity, unit cost, total cost, quantity after, average " +
            "cost after, inventory value after, and FIFO cost allocations. " +
            "The summary includes opening, period, closing, current balance, " +
            "and pending-cost values. Send one product-store ID and one item ID; " +
            "rows are ordered by MovementDate, CreatedOn, Id.";
        operation.OperationId = "InventoryCostReports_Get";
    }
}
