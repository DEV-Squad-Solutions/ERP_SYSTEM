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
                "Returns one adjustment with product store, server-derived item units, complete lines, source inventory-count ID when generated, and the base64 header row version."),
            nameof(StockAdjustmentsController.Create) => (
                "Create a stock adjustment",
                "Admin only. Creates a manual increase/decrease aggregate and its ItemMovement rows atomically. Lines send itemId, positive quantity, and optional reason. Company and item units are server-derived; decreases require sufficient chronological stock."),
            nameof(StockAdjustmentsController.Update) => (
                "Replace a stock adjustment",
                "Admin only. Replaces the complete manual adjustment and its ItemMovement rows atomically using the original eight-byte row version. Line additions, changes, and removals advance the header row version; outbound changes are stock-validated. Count-generated adjustments are immutable."),
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
