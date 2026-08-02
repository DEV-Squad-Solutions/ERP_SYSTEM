using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CashMovementTypesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(CashMovementTypesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CashMovementTypesController.GetAll) => (
                "Get paginated cash movement types",
                SwaggerOperationDescription.Create(
                    "Returns configurable voucher categories, including the independent defaults for Sales, Purchase, SalesReturn, and PurchaseReturn invoices.",
                    "Optional search, name, direction, forPartner, isActive, pageNumber, and pageSize filters.",
                    "The server derives the accounting effect: partner Receipt becomes Credit Partner, partner Payment becomes Debit Partner, and a non-partner type has no partner effect.",
                    "Deleted and other-company records are excluded.")),
            nameof(CashMovementTypesController.GetSelect) => (
                "Get active cash movement type options",
                SwaggerOperationDescription.Create(
                    "Returns active options for direct voucher forms and identifies all invoice types for which each movement type is the default.",
                    "Optional direction and forPartner filters.",
                    "Use forPartner=true when partyType is Partner. Use forPartner=false for None, Driver, or Other.",
                    "Other-company and inactive types are excluded.")),
            nameof(CashMovementTypesController.GetById) => (
                "Get a cash movement type",
                SwaggerOperationDescription.Create(
                    "Returns one company movement type.",
                    "A positive route id.",
                    "No request body.",
                    "Missing or other-company records return 404.")),
            nameof(CashMovementTypesController.Create) => (
                "Create a cash movement type",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a reusable category such as Customer Collection, Supplier Payment, Driver Advance, or Other Receipt.",
                    "Name, direction, forPartner, isActive, the four invoice-default flags, and optional notes.",
                    "The frontend must not ask for Debit or Credit. Set forPartner=true for customer/supplier types; the server derives the correct effect from direction. Sales and PurchaseReturn defaults require Receipt. Purchase and SalesReturn defaults require Payment. A default must be active and for a partner. Setting a flag replaces only the previous default for that invoice type.",
                    "Name is unique within company and direction; duplicates return 409.")),
            nameof(CashMovementTypesController.Update) => (
                "Update a cash movement type",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates using the original rowVersion.",
                    "Editable fields plus base64 rowVersion.",
                    "Direction and forPartner become immutable after use because changing them would alter historical cash or partner balances. Each of Sales, Purchase, SalesReturn, and PurchaseReturn has at most one active partner default.",
                    "Stale tokens and invalid state return 409.")),
            nameof(CashMovementTypesController.Delete) => (
                "Delete an unused cash movement type",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unused type.",
                    "A positive route id.",
                    "Used types must be deactivated to preserve voucher history.",
                    "Dependencies return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"CashMovementTypes_{context.MethodInfo.Name}";
    }
}
