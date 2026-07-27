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
                    "Returns company cash movement types with Receipt/Payment direction and optional partner debit/credit effect.",
                    "Optional search, name, direction, partnerEffect, isActive, pageNumber, and pageSize filters.",
                    "Filters use AND semantics.",
                    "Deleted and other-company records are excluded.")),
            nameof(CashMovementTypesController.GetSelect) => (
                "Get active cash movement type options",
                SwaggerOperationDescription.Create(
                    "Returns active options for voucher forms.",
                    "Optional direction and forPartner filters.",
                    "forPartner=true returns types with Debit/Credit partner effect; false returns non-partner types.",
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
                    "Admin only. Creates a user-managed Receipt or Payment type.",
                    "Name, direction, partnerEffect, isActive, and optional notes.",
                    "Name is unique within company and direction.",
                    "Duplicates return 409.")),
            nameof(CashMovementTypesController.Update) => (
                "Update a cash movement type",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates using the original rowVersion.",
                    "Editable fields plus base64 rowVersion.",
                    "Direction and partner effect become immutable after the type is used.",
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
