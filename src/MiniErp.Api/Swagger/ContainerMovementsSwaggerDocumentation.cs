using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ContainerMovementsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(ContainerMovementsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ContainerMovementsController.Create) => (
                "Create a manual container movement",
                SwaggerOperationDescription.Create(
                    "Admin or Accountant only. Adds a manual outgoing/incoming container-units movement to a container store without an invoice. The movement is operational only and has no journal, cashbox, partner, or inventory-value effect.",
                    "A bearer token containing one `company_id` with the Admin or Accountant role, plus positive `containerStoreId` and `containerId`, `movementDate`, and `outgoingUnits`/`incomingUnits` where at least one is greater than zero. `description` is optional and limited to 1,000 characters.",
                    "The container store must be an active container store linked to a business partner, the container must be active and actively assigned to that store, and the movement date must fall inside the current open fiscal year.",
                    "Missing/other-company parents return 404; product stores, inactive stores/containers, unassigned containers, a closed fiscal year, or a date outside the current fiscal year return 409. Negative units, both units zero, or an over-length description return 400. Successful creation returns 201 with `sourceType = Manual`, null invoice fields, and the current `rowVersion`.")),
            nameof(ContainerMovementsController.Update) => (
                "Update a manual container movement",
                SwaggerOperationDescription.Create(
                    "Admin or Accountant only. Replaces the container, date, units, and description of one manual movement. Invoice-generated movements are read-only. Updating a manual movement has no accounting effect.",
                    "A bearer token containing one `company_id` with the Admin or Accountant role, route `id`, `rowVersion`, and the same fields accepted by create except the container store.",
                    "Only movements with `sourceType = Manual` and no invoice can be updated. Both the existing and the requested dates must be inside open fiscal years. `rowVersion` must match the current value.",
                    "Missing/other-company movements return 404. Invoice-generated movements return 409 (`ContainerMovements.InvoiceGeneratedReadOnly`). A stale `rowVersion` returns 409 (`ContainerMovements.Concurrency`). Invalid containers, units, or dates return 400/404/409 as in create.")),
            nameof(ContainerMovementsController.Delete) => (
                "Delete a manual container movement",
                SwaggerOperationDescription.Create(
                    "Admin or Accountant only. Permanently removes one manual container movement. Invoice-generated movements are read-only. The delete affects container-unit balances only; no journal, cashbox, partner, or inventory-value rows change.",
                    "A bearer token containing one `company_id` with the Admin or Accountant role, route `id`, and query `rowVersion`.",
                    "Only movements with `sourceType = Manual` and no invoice can be deleted, and the movement's fiscal year must be open. `rowVersion` must match the current value.",
                    "Missing/other-company movements return 404. Invoice-generated movements return 409 (`ContainerMovements.InvoiceGeneratedReadOnly`). A stale `rowVersion` returns 409 (`ContainerMovements.Concurrency`). Success returns 204.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"ContainerMovements_{context.MethodInfo.Name}";
    }
}
