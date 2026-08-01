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
                    "Returns active manual and invoice-generated Receipt and Payment vouchers for the selected company. InvoiceId and InvoiceNumber identify generated payment vouchers.",
                    "Optional search, voucherNumber, direction, cashboxId, cashMovementTypeId, partyType, businessPartnerId, driverId, driverTripId, fromDate, toDate, pageNumber, and pageSize filters.",
                    "Search covers voucher, cashbox, movement type, party, trip invoice, reference, and description display values.",
                    "Deleted and other-company vouchers are excluded.")),
            nameof(CashVouchersController.GetById) => (
                "Get a cash voucher",
                SwaggerOperationDescription.Create(
                    "Returns one voucher with its draft status, optional cashbox, optional movement type, party, optional source invoice, transaction/base amounts, resolved exchange-rate snapshot, and concurrency token.",
                    "A positive route id.",
                    "CompanyId, currency, and partner Debit/Credit are server-controlled. The frontend does not send or display an accounting-side selector.",
                    "Missing or other-company vouchers return 404.")),
            nameof(CashVouchersController.Create) => (
                "Create a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. A first save can create a draft; a completed voucher and its cash/partner effects are saved atomically.",
                    "The first save requires only `voucherDate`, Receipt or Payment `direction`, a positive `amount`, and optional `notes`. `voucherNumber` is generated automatically when omitted. Omit both `cashboxId` and `cashMovementTypeId` to save a draft.",
                    "To post immediately, provide both an active cashbox and matching active movement type together, plus the applicable party fields. An optional positive `exchangeRate` overrides automatic dated resolution.",
                    "Drafts create no cashbox or partner movement. Completed partner Receipt is Credit and Payment is Debit; Payment cannot make the cashbox negative.")),
            nameof(CashVouchersController.Update) => (
                "Update a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces a manual draft or completed voucher and all derived effects, including its base-currency snapshot, atomically.",
                    "Editable fields plus the original base64 rowVersion. Voucher number is server-generated and immutable.",
                    "Providing both cashbox and movement type completes a draft. Omitting both keeps it as a draft. The old effects are removed and the new effects are applied exactly once.",
                    "A stale token returns CashVouchers.Concurrency. An invoice-generated voucher returns CashVouchers.InvoiceGeneratedReadOnly and must be changed through its invoice.")),
            nameof(CashVouchersController.Delete) => (
                "Delete a cash voucher",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes a manual voucher and its partner movement atomically.",
                    "A positive route id.",
                    "Deleting a receipt is rejected when it would leave its cashbox negative.",
                    "Missing records return 404; concurrency and balance conflicts return 409. Invoice-generated vouchers are read-only and return CashVouchers.InvoiceGeneratedReadOnly.")),
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
