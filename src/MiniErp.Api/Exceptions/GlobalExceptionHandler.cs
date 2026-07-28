using Microsoft.AspNetCore.Diagnostics;
using MiniErp.Api.Errors;

namespace MiniErp.Api.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var response = ApiErrorResponseFactory.Unexpected(httpContext);

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            response.TraceId);

        await ApiErrorResponseFactory.WriteAsync(
            httpContext,
            response,
            cancellationToken);

        return true;
    }
}
