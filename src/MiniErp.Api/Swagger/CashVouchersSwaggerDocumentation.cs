using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CashVouchersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CashVouchersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CashVouchersController.GetAll) => (
                "Get paginated cash vouchers",
                SwaggerOperationDescription.Create(
                    "Returns active Receipt and Payment vouchers for the selected company.",
                    "Optional search, voucherNumber, direction, cashboxId, cashMovementTypeId, partyType, businessPartnerId, driverId, driverTripId, fromDate, toDate, pageNumber, and pageSize filters.",
                    "Search covers voucher, cashbox, movement type, party, trip invoice, reference, and description display values.",
                    "Deleted and other-company vouchers are excluded.")),
            nameof(CashVouchersController.GetById) => (
                "Get a cash voucher",
                SwaggerOperationDescription.Create(
                    "Returns one voucher with cashbox, type, party, derived currency, and concurrency token.",
                    "A positive route id.",
                    "CompanyId and currency are server-controlled.",
                    "Missing or other-company vouchers return 404.")),
            nameof(CashVouchersController.Create) => (
                "Create a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. Saves the voucher immediately and creates exactly one existing-table partner movement when PartyType is Partner.",
                    "Voucher number/date, Receipt or Payment, active cashbox, matching active movement type, party fields, positive amount, and optional reference/description/notes.",
                    "DriverTripId is always optional and a voucher never creates a trip. Partner currency must match cashbox currency. Payment cannot make the cashbox balance negative.",
                    "Relationship failures return 404/409; the full operation is atomic.")),
            nameof(CashVouchersController.Update) => (
                "Update a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces voucher values and its partner effect atomically.",
                    "Editable fields plus the original base64 rowVersion.",
                    "The old cashbox/partner effect is removed and the new effect is applied exactly once.",
                    "A stale token returns CashVouchers.Concurrency.")),
            nameof(CashVouchersController.Delete) => (
                "Delete a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes the voucher and its partner movement atomically.",
                    "A positive route id.",
                    "Deleting a receipt is rejected when it would leave its cashbox negative.",
                    "Missing records return 404; concurrency and balance conflicts return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"CashVouchers_{context.MethodInfo.Name}";
    }
}
