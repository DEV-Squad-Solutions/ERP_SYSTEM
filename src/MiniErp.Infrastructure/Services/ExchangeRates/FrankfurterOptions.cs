namespace MiniErp.Infrastructure.Services.ExchangeRates;

public sealed class FrankfurterOptions
{
    public const string SectionName = "Frankfurter";

    public string BaseUrl { get; init; } =
        "https://api.frankfurter.dev";

    public string Provider { get; init; } = "CBE";

    public int TimeoutSeconds { get; init; } = 15;
}
