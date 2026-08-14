using MiniErp.Api.Errors;

namespace MiniErp.Api.Startup;

public sealed class DatabaseReadinessMiddleware(
    RequestDelegate next,
    StartupDatabaseStatus status)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (status.GetSnapshot().IsReady || IsDiagnosticPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var response = ApiErrorResponseFactory.DatabaseUnavailable(context);
        context.Response.Headers.RetryAfter = "15";
        await ApiErrorResponseFactory.WriteAsync(
            context,
            response,
            context.RequestAborted);
    }

    private static bool IsDiagnosticPath(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/swagger");
}
