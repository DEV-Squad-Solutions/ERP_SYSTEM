using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StatementsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(StatementsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StatementsController.GetCashboxStatement) => (
                "Get Cashbox Statement",
                SwaggerOperationDescription.Create(
                    "Returns CashVoucher rows with opening, receipt, payment, running, and closing balances.",
                    "Required cashboxId plus optional search, date, direction, movement type, party, voucher, and pagination filters.",
                    "Opening balance includes all active cashbox movements before fromDate. Rows are ordered by date, creation time, voucher number, and id.",
                    "Missing or other-company cashboxes return 404.")),
            nameof(StatementsController.GetPartnerStatement) => (
                "Get Partner Statement",
                SwaggerOperationDescription.Create(
                    "Combines partner opening balances and existing BusinessPartnerMovement rows from invoices and CashVouchers exactly once.",
                    "Required businessPartnerId plus optional search, date, sourceType, movementType, cashMovementTypeId, and pagination filters.",
                    "Debit minus credit is the existing partner balance convention.",
                    "Missing or other-company partners return 404.")),
            nameof(StatementsController.GetDriverStatement) => (
                "Get Driver Statement",
                SwaggerOperationDescription.Create(
                    "Combines driver CashVouchers with operational DriverTrip costs, including vouchers without trips.",
                    "Required driverId plus optional search, date, direction, movement type, trip, invoice, without-trip, has-cost, and pagination filters.",
                    "Running balance is cash paid to driver minus cash returned minus DriverTrip cost. General vouchers are never auto-allocated to a trip.",
                    "Missing or other-company drivers return 404.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Statements_{context.MethodInfo.Name}";
    }
}
