using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CashboxTransfersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(CashboxTransfersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CashboxTransfersController.GetAll) => (
                "Get cashbox transfers",
                SwaggerOperationDescription.Create(
                    "Returns current-company cashbox transfers ordered by transfer date and ID descending.",
                    "Supports search, source/destination cashbox, and inclusive date filters.",
                    "Only active transfer records are returned.",
                    "Invalid pagination or filters return 400.")),
            nameof(CashboxTransfersController.GetById) => (
                "Get a cashbox transfer",
                SwaggerOperationDescription.Create(
                    "Returns the transfer with its generated payment and receipt vouchers.",
                    "Requires an authenticated current-company context and a positive ID.",
                    "Other-company and deleted transfers are not visible.",
                    "Missing records return 404.")),
            nameof(CashboxTransfersController.Create) => (
                "Create a cashbox transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Atomically creates a payment in the source cashbox and a receipt in the destination cashbox.",
                    "Requires two different active company-owned cashboxes with the same currency and a positive amount.",
                    "The source cashbox balance cannot become negative. Generated vouchers cannot be edited independently.",
                    "Cross-currency transfers return 409 until an exchange-difference policy is configured.")),
            nameof(CashboxTransfersController.Update) => (
                "Update a cashbox transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Atomically updates the transfer and both generated vouchers.",
                    "Requires the current 8-byte rowVersion and the complete desired transfer values.",
                    "All affected cashbox balances must remain non-negative.",
                    "Stale versions and balance conflicts return 409.")),
            nameof(CashboxTransfersController.Delete) => (
                "Delete a cashbox transfer",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes the transfer and both generated vouchers atomically.",
                    "Requires a positive transfer ID.",
                    "Removing the destination receipt must not make its balance negative.",
                    "Missing records return 404; balance conflicts return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"CashboxTransfers_{context.MethodInfo.Name}";
    }
}
