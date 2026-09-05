using System.Diagnostics.Metrics;

namespace MiniErp.Infrastructure.Services.Monitoring;

internal static class ReportingMetrics
{
    internal const string MeterName = "MiniErp.Reporting";

    private static readonly Meter Meter = new(MeterName);

    internal static readonly Histogram<double> DashboardDuration =
        Meter.CreateHistogram<double>(
            "mini_erp.dashboard.duration",
            unit: "ms",
            description: "Time spent building a dashboard response.");

    internal static readonly Histogram<double> ProfitabilityDuration =
        Meter.CreateHistogram<double>(
            "mini_erp.profitability.duration",
            unit: "ms",
            description: "Time spent loading profitability report data.");

    internal static readonly Histogram<long> ProfitabilityLoadedLines =
        Meter.CreateHistogram<long>(
            "mini_erp.profitability.loaded_lines",
            unit: "{line}",
            description: "Number of report lines loaded for profitability calculations.");
}
