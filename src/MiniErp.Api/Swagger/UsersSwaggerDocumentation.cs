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
                "Admin only. Returns users with all assigned roles and active companies. Page size is limited to 100."),
            nameof(UsersController.GetRoles) => (
                "Get available roles",
                "Admin only. Returns Identity role names for user-management controls."),
            nameof(UsersController.GetById) => (
                "Get a user",
                "Admin only. Returns one user with all assigned roles and active companies."),
            nameof(UsersController.Create) => (
                "Create a user",
                "Admin only. Creates an Identity user, assigns one or more roles, and grants access to one or more active companies in a single transaction."),
            nameof(UsersController.Update) => (
                "Update a user",
                "Admin only. Updates profile information and synchronizes all role and company assignments without changing the password."),
            nameof(UsersController.AssignCompanies) => (
                "Assign companies to a user",
                "Admin only. Replaces the user's complete company-access set. At least one active company is required. The user must log out and log in again to start a session in another company."),
            nameof(UsersController.Delete) => (
                "Delete a user",
                "Admin only. Permanently deletes the Identity user and related assignments. Self-deletion, deletion of the last Admin, and removal of the last Admin role are blocked."),
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
