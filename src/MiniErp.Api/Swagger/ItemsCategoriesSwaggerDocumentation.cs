using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ItemsCategoriesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(ItemsCategoriesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ItemsCategoriesController.GetAll) => (
                "Get paginated item categories",
                SwaggerOperationDescription.Create(
                    "Returns item categories owned by the selected company.",
                    "Optional search, name, isActive, pageNumber, and pageSize filters.",
                    "Each item includes the current base64 rowVersion.",
                    "Deleted and other-company categories are excluded.")),
            nameof(ItemsCategoriesController.GetSelect) => (
                "Get active item category options",
                SwaggerOperationDescription.Create(
                    "Returns active company item categories for invoice headers.",
                    "No request body.",
                    "Use the returned id as itemsCategoryId; the selection is optional.",
                    "Inactive, deleted, and other-company categories are excluded.")),
            nameof(ItemsCategoriesController.GetById) => (
                "Get an item category",
                SwaggerOperationDescription.Create(
                    "Returns one category owned by the selected company.",
                    "A positive route id.",
                    "No request body.",
                    "Missing or other-company records return 404.")),
            nameof(ItemsCategoriesController.Create) => (
                "Create an item category",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates a reusable invoice-header item category.",
                    "Name, isActive, and optional notes.",
                    "Audit values and rowVersion are server-controlled.",
                    "An active duplicate name in the same company returns 409.")),
            nameof(ItemsCategoriesController.Update) => (
                "Update an item category",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates a category using optimistic concurrency.",
                    "Editable fields plus the original base64 rowVersion.",
                    "Send the exact token returned by the API.",
                    "Stale tokens and active duplicate names return 409.")),
            nameof(ItemsCategoriesController.Delete) => (
                "Delete an unused item category",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unused category.",
                    "A positive route id.",
                    "Categories referenced by current or historical invoices must be deactivated instead.",
                    "Invoice dependencies return 409.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"ItemsCategories_{context.MethodInfo.Name}";
    }
}
