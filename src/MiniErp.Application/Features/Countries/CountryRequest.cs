namespace MiniErp.Application.Features.Countries;

public sealed record CountryRequest(
    string Name,
    string ArabicName,
    bool IsActive = true);
