using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StockAdjustmentsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(StockAdjustmentsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StockAdjustmentsController.GetAll) => (
                "Get paginated stock adjustments",
                "Returns tenant-isolated increase/decrease documents ordered by document date and ID. Every list item includes its complete deterministically ordered line collection."),
            nameof(StockAdjustmentsController.GetById) => (
                "Get a stock adjustment",
                "Returns one adjustment with complete lines, entered increase cost, movement cost status, pending quantity, server-calculated total cost, quantity after, average cost after, inventory value after, and the base64 header row version."),
            nameof(StockAdjustmentsController.Create) => (
                "Create a stock adjustment",
                "Admin only. Creates a manual increase/decrease aggregate, movements, cost snapshots, pending allocations, and item/store balances in one Serializable transaction. Increase lines require nonnegative `unitCost`; decrease lines must omit `unitCost` because their cost is server-calculated from the current weighted average."),
            nameof(StockAdjustmentsController.Update) => (
                "Replace a stock adjustment",
                "Admin only. Replaces the complete manual adjustment using the original eight-byte row version. Matching movements retain ID and CreatedOn; every affected timeline is replayed. Line additions, changes, and removals advance the header token; count-generated adjustments are immutable."),
            nameof(StockAdjustmentsController.Delete) => (
                "Delete a stock adjustment",
                "Admin only. Soft-deletes a manual adjustment, its lines, and its adjustment movements atomically after validating the complete affected stock timeline. Generated adjustments cannot be deleted."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"StockAdjustments_{context.MethodInfo.Name}";
    }
}
