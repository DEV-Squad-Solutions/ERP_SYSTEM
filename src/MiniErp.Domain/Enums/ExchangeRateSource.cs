namespace MiniErp.Domain.Enums;

public enum ExchangeRateSource
{
    Manual = 1,
    Imported = 2,

    /// <summary>
    /// Created automatically on a fiscal year's start date from the latest
    /// rate of the previous period; refreshed while no document uses it.
    /// </summary>
    CarriedForward = 3
}
