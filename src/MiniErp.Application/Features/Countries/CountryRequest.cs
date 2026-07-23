namespace MiniErp.Application.Features.Countries;

public sealed record CountryRequest(
    string Code,
    string Name,
    string ArabicName,
    bool IsActive = true);
