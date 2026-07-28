namespace MiniErp.Application.Features.Countries;

public sealed record CountryFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    string? ArabicName = null,
    bool? IsActive = null);
