using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Api.Monitoring;

namespace MiniErp.Tests.Monitoring;

public sealed class RequestMonitoringMiddlewareTests
{
    [Fact]
    public async Task Middleware_EmitsDurationAndFailureMetrics()
    {
        var observed = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name ==
                RequestMonitoringMiddleware.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.Start();

        var middleware = new RequestMonitoringMiddleware(
            context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            },
            NullLogger<RequestMonitoringMiddleware>.Instance);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Contains("mini_erp.http.request.duration", observed);
        Assert.Contains("mini_erp.http.request.failures", observed);
    }
}
