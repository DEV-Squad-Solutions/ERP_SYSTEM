namespace MiniErp.Application.Features.Countries;

public sealed record CountryFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    string? EnglishName = null,
    bool? IsActive = null);
