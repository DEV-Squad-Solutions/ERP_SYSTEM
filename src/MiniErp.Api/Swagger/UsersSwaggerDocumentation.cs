using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class UsersSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(UsersController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(UsersController.GetAll) => (
                "Get paginated users",
                SwaggerOperationDescription.Create(
                    "Admin only. Returns users with all assigned roles and active companies, ordered by username and ID.",
                    "An Admin bearer token. Pagination fields are optional and default to page 1 with 20 items.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. A page beyond the result set returns an empty item list with valid metadata.")),
            nameof(UsersController.GetRoles) => (
                "Get available roles",
                SwaggerOperationDescription.Create(
                    "Admin only. Returns existing Identity role names for user-management controls.",
                    "An Admin bearer token.",
                    "No request fields.",
                    "Returns an empty array when no named roles exist. Role names are ordered.")),
            nameof(UsersController.GetById) => (
                "Get a user",
                SwaggerOperationDescription.Create(
                    "Admin only. Returns one user with all assigned roles and active companies.",
                    "An Admin bearer token and route `id` as a GUID.",
                    "`id` must be a non-empty GUID.",
                    "An empty ID returns 400; an invalid route format does not match the endpoint; a missing user returns 404.")),
            nameof(UsersController.Create) => (
                "Create a user",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates an Identity user, assigns roles, and grants company access in one transaction.",
                    "`userName`, `email`, `firstName`, `lastName`, `password`, one or more `roles`, and one or more `companyIds`. Phone is optional.",
                    "Username/email max 256; names max 100; phone max 50; email format must be valid; password length is 8-128 plus Identity policy. Roles are non-empty, unique case-insensitively, and max 256 each. Company IDs are positive and unique.",
                    "Duplicate username or email returns 409. Unknown roles or missing/deleted companies return 404. Any Identity, role, company, or save failure rolls back the complete operation.")),
            nameof(UsersController.Update) => (
                "Update a user",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates profile information and replaces all role and company assignments without changing the password.",
                    "An Admin bearer token, non-empty GUID route `id`, profile fields, one or more `roles`, and one or more `companyIds`. Phone is optional.",
                    "Create validation applies except no password is accepted. Duplicate checks exclude the current user.",
                    "Missing users, roles, or companies return 404; duplicate username/email returns 409. Removing Admin from the final Admin account is blocked with 409. Changes are synchronized transactionally.")),
            nameof(UsersController.AssignCompanies) => (
                "Assign companies to a user",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces the user's complete company-access set.",
                    "An Admin bearer token, non-empty GUID route `id`, and one or more `companyIds`.",
                    "Company IDs must be positive, unique, and reference non-deleted companies.",
                    "An empty ID returns 400; missing users or companies return 404. Existing access tokens retain their selected company until expiry or logout, so the user must log in again to select another company.")),
            nameof(UsersController.Delete) => (
                "Delete a user",
                SwaggerOperationDescription.Create(
                    "Admin only. Permanently deletes an Identity user and related assignments.",
                    "An Admin bearer token and route `id` as a non-empty GUID.",
                    "`id` must be a non-empty GUID.",
                    "Missing users return 404. Self-deletion and deletion of the final Admin account return 409. Unlike ERP master data, this operation is a physical Identity deletion.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Users_{context.MethodInfo.Name}";
    }
}
