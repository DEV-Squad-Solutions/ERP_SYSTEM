using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AuthenticationSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(AuthController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(AuthController.Login) => (
                "Log in",
                "Validates credentials and returns all assigned Identity roles. A user with one company receives access and refresh tokens immediately. A user with multiple companies receives a five-minute selection token and must call select-company."),
            nameof(AuthController.SelectCompany) => (
                "Select the session company",
                "Validates the short-lived selection token and company assignment, then issues access and refresh tokens containing exactly one company_id claim."),
            nameof(AuthController.Refresh) => (
                "Refresh the selected-company session",
                "Rotates a valid refresh token while preserving its selected company. Refresh fails when the user no longer has access to that company."),
            nameof(AuthController.Logout) => (
                "Log out",
                "Revokes the supplied refresh token. The operation is idempotent."),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Authentication_{context.MethodInfo.Name}";
    }
}
