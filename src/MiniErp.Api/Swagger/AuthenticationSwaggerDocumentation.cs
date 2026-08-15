using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AuthenticationSwaggerDocumentation(
    IConfiguration configuration) : IOperationFilter
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
                SwaggerOperationDescription.Create(
                    "Validates credentials and returns all assigned Identity roles. A user with one company receives access and refresh tokens immediately. A user with multiple companies must complete company selection.",
                    "`userName` and `password`.",
                    "Both values must be non-empty. Username whitespace is trimmed before lookup; the password is used exactly as supplied.",
                    "Invalid credentials or a locked account return 401. A user without an active company assignment returns 403. Multiple companies return a five-minute selection token instead of access and refresh tokens.")),
            nameof(AuthController.SelectCompany) => (
                "Select the session company",
                SwaggerOperationDescription.Create(
                    "Validates the short-lived selection token and company assignment, then issues access and refresh tokens containing exactly one company_id claim.",
                    "`selectionToken` and `companyId`.",
                    "The token must be non-empty and `companyId` must be greater than zero.",
                    "An invalid, expired, wrong-purpose, or already-invalidated selection token returns 401. Selecting an unassigned company returns 403. The token expires after five minutes.")),
            nameof(AuthController.Refresh) => (
                "Refresh the selected-company session",
                SwaggerOperationDescription.Create(
                    "Rotates a valid refresh token while preserving its selected company.",
                    "`refreshToken`.",
                    "The refresh token must be non-empty.",
                    "Expired, revoked, concurrently reused, or unknown tokens return 401. Refresh also fails when the user is locked out or no longer has access to the token's company. A successful refresh revokes the supplied token.")),
            nameof(AuthController.Logout) => (
                "Log out",
                SwaggerOperationDescription.Create(
                    "Revokes the supplied refresh token. The operation is idempotent.",
                    "`refreshToken`.",
                    "The refresh token must be non-empty.",
                    "An unknown or already-revoked token still returns 204. Concurrent revocation also completes successfully.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Authentication_{context.MethodInfo.Name}";

        if (context.MethodInfo.Name == nameof(AuthController.Login))
        {
            ApplySeededLoginExample(operation);
        }
    }

    private void ApplySeededLoginExample(OpenApiOperation operation)
    {
        if (!configuration.GetValue("Seed:Enabled", false))
        {
            return;
        }

        var password = configuration["Seed:Password"];
        var content = operation.RequestBody?.Content;
        if (string.IsNullOrWhiteSpace(password) ||
            content is null ||
            !content.TryGetValue("application/json", out var mediaType) ||
            mediaType is null)
        {
            return;
        }

        mediaType.Example = new JsonObject
        {
            ["userName"] = "admin",
            ["password"] = password
        };
    }
}
