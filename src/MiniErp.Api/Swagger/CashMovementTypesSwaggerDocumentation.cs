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
                    "Optional search, name, direction, classification, forPartner, isActive, pageNumber, and pageSize filters.",
                    "The server derives the accounting effect: partner Receipt becomes Credit Partner, partner Payment becomes Debit Partner, and a non-partner type has no partner effect.",
                    "Deleted and other-company records are excluded.")),
            nameof(CashMovementTypesController.GetSelect) => (
                "Get active cash movement type options",
                SwaggerOperationDescription.Create(
                    "Returns active options for direct voucher forms and identifies all invoice types for which each movement type is the default.",
                    "Optional direction, classification, and forPartner filters.",
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
                    "Name, direction, required classification, forPartner, isActive, the four invoice-default flags, and optional notes.",
                    "Classification is direction-neutral: Expense and Revenue may be Receipt or Payment and may be linked to a partner. PartnerSettlement requires forPartner=true. Invoice defaults must be active PartnerSettlement types with their required direction. The server derives Debit or Credit from direction.",
                    "Name is unique within company and direction; duplicates return 409.")),
            nameof(CashMovementTypesController.Update) => (
                "Update a cash movement type",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates using the original rowVersion.",
                    "Editable fields plus base64 rowVersion.",
                    "Direction, classification, and forPartner become immutable after use because changing them would alter historical filtering, cash, or partner balances. Each invoice default must be an active PartnerSettlement type.",
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
