namespace MiniErp.Application.Features.Countries;

public sealed record CountryResponse(
    int Id,
    string Code,
    string Name,
    string EnglishName,
    bool IsActive);
