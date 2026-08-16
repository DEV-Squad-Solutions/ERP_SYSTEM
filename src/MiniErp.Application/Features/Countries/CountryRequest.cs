namespace MiniErp.Application.Features.Countries;

public sealed record CountryRequest(
    string Name,
    string EnglishName,
    bool IsActive = true);
