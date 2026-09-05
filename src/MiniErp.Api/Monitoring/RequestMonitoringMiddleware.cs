using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace MiniErp.Api.Monitoring;

public sealed class RequestMonitoringMiddleware(
    RequestDelegate next,
    ILogger<RequestMonitoringMiddleware> logger)
{
    public const string MeterName = "MiniErp.Api";

    private static readonly Meter ApiMeter = new(MeterName);
    private static readonly Histogram<double> RequestDuration =
        ApiMeter.CreateHistogram<double>(
            "mini_erp.http.request.duration",
            unit: "ms",
            description: "HTTP request duration by route and status code.");
    private static readonly Counter<long> FailedRequests =
        ApiMeter.CreateCounter<long>(
            "mini_erp.http.request.failures",
            unit: "{request}",
            description: "HTTP requests that ended with a server error.");

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        Exception? exception = null;
        try
        {
            await next(context);
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var statusCode = exception is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;
            var route = (context.GetEndpoint() as RouteEndpoint)?
                .RoutePattern.RawText ?? "unmatched";
            var tags = new TagList
            {
                { "http.request.method", context.Request.Method },
                { "http.response.status_code", statusCode },
                { "http.route", route }
            };
            RequestDuration.Record(elapsed.TotalMilliseconds, tags);

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                FailedRequests.Add(1, tags);
                logger.LogWarning(
                    exception,
                    "Request {Method} {Route} failed with {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    route,
                    statusCode,
                    elapsed.TotalMilliseconds,
                    context.TraceIdentifier);
            }
            else if (elapsed >= TimeSpan.FromSeconds(2))
            {
                logger.LogWarning(
                    "Slow request {Method} {Route} completed with {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    route,
                    statusCode,
                    elapsed.TotalMilliseconds,
                    context.TraceIdentifier);
            }
        }
    }
}
