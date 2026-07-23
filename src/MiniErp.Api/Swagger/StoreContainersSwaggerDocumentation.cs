using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StoreContainersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(StoreContainersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StoreContainersController.GetAll) => (
                "Get paginated store-container assignments",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic page of non-deleted container assignments for the selected company, ordered by store name, container name, and assignment ID. Store, partner, and container display fields are projected in one database query.",
                    "A bearer token containing one `company_id`. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set is empty. Active and inactive assignments are included; deleted and other-company assignments are excluded.")),
            nameof(StoreContainersController.GetSelect) => (
                "Get containers assigned to a store",
                SwaggerOperationDescription.Create(
                    "Returns active, non-deleted, active-container assignments for one usable container store. Each result uses the container ID, not the assignment ID, with the container name.",
                    "A bearer token containing one `company_id` and query `storeId`.",
                    "`storeId` must be greater than zero and identify an active container store with an active business partner in the selected company.",
                    "Invalid IDs return 400 (`StoreContainers.InvalidStoreId`); missing, deleted, and other-company stores return 404 (`StoreContainers.StoreNotFound`). Product stores, inactive stores, or stores with an inactive partner return 409 (`StoreContainers.StoreNotContainerStore`, `StoreContainers.StoreInactive`, or `StoreContainers.StoreBusinessPartnerInactive`). Returns an empty array when no usable active assignment exists.")),
            nameof(StoreContainersController.GetById) => (
                "Get a store-container assignment",
                SwaggerOperationDescription.Create(
                    "Returns one non-deleted assignment in the selected company, including store, partner, and container display fields.",
                    "A bearer token containing one `company_id` and route `id`.",
                    "`id` must be greater than zero.",
                    "Invalid IDs return 400 (`StoreContainers.InvalidId`). Missing, deleted, and other-company assignments return 404 (`StoreContainers.NotFound`).")),
            nameof(StoreContainersController.Upsert) => (
                "Upsert a store's container assignments",
                SwaggerOperationDescription.Create(
                    "Admin only. Atomically replaces one container Store's complete active Container set in the selected company. Existing selected rows remain unchanged, selected inactive rows are reactivated, new selections are created, omitted rows are deactivated and soft-deleted, and deleted history is never restored.",
                    "Positive `storeId` and a present `containerIds` array containing the complete desired active set. Send an empty array to remove every current assignment. Do not send assignment IDs, `isActive`, or `companyId`.",
                    "`containerIds` must contain at most 100 unique positive IDs. Every selected Container must be active and belong to the selected company. The Store must belong to the selected company and be a container Store. A non-empty list also requires an active Store and active linked BusinessPartner; an empty list may clear an inactive Store.",
                    "Missing or other-company parents return 404 (`StoreContainers.StoreNotFound` or `StoreContainers.ContainerNotFound`). Invalid parent states return 409 (`StoreContainers.StoreNotContainerStore`, `StoreContainers.StoreInactive`, `StoreContainers.StoreBusinessPartnerInactive`, or `StoreContainers.ContainerInactive`). Invalid, duplicate, missing, or over-limit `containerIds` return 400 validation errors. The full change uses one serializable transaction; any failure leaves the previous set unchanged. Repeating the same request is idempotent and makes no audit write. Soft-deleted history continues to protect Store and Container deletion.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"StoreContainers_{context.MethodInfo.Name}";
    }
}
