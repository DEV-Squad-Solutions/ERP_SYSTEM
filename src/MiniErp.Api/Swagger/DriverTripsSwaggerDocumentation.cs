using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class DriverTripsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(DriverTripsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(DriverTripsController.GetCostEntry) => (
                "Get DriverTrip cost-entry rows",
                SwaggerOperationDescription.Create(
                    "Returns invoice-created trips for later operational cost entry.",
                    "Optional search, date range, driverId, invoiceNumber, tripNumber, hasCost, pageNumber, and pageSize filters.",
                    "Trip numbers use TR-{DriverTripId}. Cost is operational and does not change cashbox, partner, or invoice values.",
                    "Other-company and deleted trips are excluded.")),
            nameof(DriverTripsController.UpdateCosts) => (
                "Bulk update DriverTrip costs",
                SwaggerOperationDescription.Create(
                    "Admin only. Validates every row and updates all costs atomically.",
                    "One to 100 unique items containing driverTripId, nullable non-negative cost, optional notes, and the original base64 rowVersion.",
                    "One invalid, missing, other-company, or stale row rejects the complete request.",
                    "No CashVoucher, cashbox effect, partner movement, invoice update, or new DriverTrip is created.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"DriverTrips_{context.MethodInfo.Name}";
    }
}
