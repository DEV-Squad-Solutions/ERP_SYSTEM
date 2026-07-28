using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CashboxesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CashboxesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(CashboxesController.GetAll) => (
                "Get paginated cashboxes",
                SwaggerOperationDescription.Create(
                    "Returns cashboxes owned by the selected company with server-calculated current balances.",
                    "A bearer token with one company_id. Optional filters: search, code, name, currency, isActive, pageNumber, and pageSize.",
                    "Search and typed filters are combined with AND before deterministic pagination.",
                    "Deleted and other-company cashboxes are excluded.")),
            nameof(CashboxesController.GetSelect) => (
                "Get active cashbox options",
                SwaggerOperationDescription.Create(
                    "Returns active cashboxes with currency and current balance for voucher forms.",
                    "An authenticated bearer token with one company_id.",
                    "No request body.",
                    "Deleted, inactive, and other-company cashboxes are excluded.")),
            nameof(CashboxesController.GetById) => (
                "Get a cashbox",
                SwaggerOperationDescription.Create(
                    "Returns one cashbox and its derived current balance.",
                    "A positive route id and an authenticated company context.",
                    "No request body.",
                    "Missing, deleted, and other-company records return 404.")),
            nameof(CashboxesController.Create) => (
                "Create a cashbox",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a company cashbox without storing a mutable current balance.",
                    "Code, name, currency, openingBalance, isActive, and optional notes.",
                    "Code and name are unique within the selected company.",
                    "Duplicates return 409.")),
            nameof(CashboxesController.Update) => (
                "Update a cashbox",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a cashbox using optimistic concurrency.",
                    "Editable fields plus the original base64 rowVersion.",
                    "Opening balance and currency cannot change after any current or historical voucher exists.",
                    "A stale token returns Cashboxes.Concurrency and requires reload.")),
            nameof(CashboxesController.Delete) => (
                "Delete an unused cashbox",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unused cashbox.",
                    "A positive route id.",
                    "Cashboxes referenced by current or historical vouchers cannot be deleted; deactivate them instead.",
                    "Dependencies return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Cashboxes_{context.MethodInfo.Name}";
    }
}
