using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class InventoryCountsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(InventoryCountsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(InventoryCountsController.GetAll) => (
                "Get paginated inventory counts",
                "Returns tenant-isolated count headers with completion counts, reconciliation state, generated adjustment IDs, and the base64 header row version."),
            nameof(InventoryCountsController.GetById) => (
                "Get an inventory count",
                "Returns the immutable stock snapshot and complete ordered item lines. Difference is calculated as physicalQuantity minus systemQuantity."),
            nameof(InventoryCountsController.Create) => (
                "Create an inventory count snapshot",
                "Admin only. Validates an active product store, loads every active company item whose unit is active (including zero-stock items), calculates stock through countDate, and freezes the snapshot atomically."),
            nameof(InventoryCountsController.Update) => (
                "Enter physical inventory quantities",
                "Admin only. Replaces physical quantities and notes for the complete frozen item set using the original eight-byte row version. Store, date, items, units, and system quantities cannot change."),
            nameof(InventoryCountsController.Reconcile) => (
                "Reconcile an inventory count",
                "Admin only. Requires every physical quantity and the current row version. Rejects a stale snapshot, creates at most one Increase and one Decrease StockAdjustment, omits zero-difference lines and empty adjustments, writes ItemMovement rows, and marks the count reconciled in one transaction."),
            nameof(InventoryCountsController.Delete) => (
                "Delete an unreconciled inventory count",
                "Admin only. Soft-deletes only an unreconciled count and its lines. Reconciled counts and generated adjustments are immutable; corrections require a new count or manual adjustment."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"InventoryCounts_{context.MethodInfo.Name}";
    }
}
