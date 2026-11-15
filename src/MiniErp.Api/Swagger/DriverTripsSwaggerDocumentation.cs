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
            nameof(DriverTripsController.Create) => (
                "Create standalone driver trip",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a trip without an invoice in the explicitly selected open fiscal year.",
                    "Requires an active same-company driver, a date in that year, and optional active same-company businessPartnerId, actualDriverName, cost and notes.",
                    "Cost accepts an exact decimal string, from zero to 999999999999999.99 in company base currency. Positive cost posts expense debit and driver-control credit; zero/null removes the source journal.",
                    "Returns the cost-entry row including nullable invoice and partner fields, isStandalone, isReadOnly, baseCurrency, actualDriverName, exact costText and Base64 rowVersion.")),
            nameof(DriverTripsController.GetById) => (
                "Get driver trip in selected fiscal year",
                SwaggerOperationDescription.Create(
                    "Reads one same-company trip in fiscalYearId, defaulting to the current fiscal year.",
                    "Route id; optional fiscalYearId query.",
                    "Other-company and other-year trips return 404.",
                    "Closed years are readable and marked isReadOnly.")),
            nameof(DriverTripsController.Update) => (
                "Update standalone driver trip",
                SwaggerOperationDescription.Create(
                    "Admin only. Requires the latest Base64 rowVersion. fiscalYearId is immutable; invoice-owned trips use their invoice or bulk cost entry.",
                    "Same fields as creation plus rowVersion.",
                    "Current or historical linked cash vouchers prevent driver, partner or date changes; cost and notes can still change.",
                    "Updates the single automatic source journal atomically.")),
            nameof(DriverTripsController.Delete) => (
                "Delete standalone driver trip",
                SwaggerOperationDescription.Create(
                    "Admin only. Requires the latest Base64 rowVersion query parameter. Deletes the source journal and standalone trip in one transaction.",
                    "Route id; required Base64 rowVersion query.",
                    "Closed years, invoice-owned trips and any current or historical linked cash voucher prevent deletion.",
                    "Successful deletion emits DriverTrip realtime after commit.")),
            nameof(DriverTripsController.GetCostEntry) => (
                "Get DriverTrip cost-entry rows",
                SwaggerOperationDescription.Create(
                    "Returns invoice-created and standalone trips. Invoice, partner and country fields may be null. Exact costText supports safe decimal editing.",
                    "Optional fiscalYearId, isStandalone, search, date range, driverId, invoiceNumber, tripNumber, hasCost, pageNumber, and pageSize filters.",
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
