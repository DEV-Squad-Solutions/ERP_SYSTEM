namespace MiniErp.Application.Features.Drivers;

public sealed record DriverFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    string? LicenseNumber = null,
    bool? IsActive = null,
    bool? HasExpiredLicense = null,
    DateOnly? LicenseExpiryFrom = null,
    DateOnly? LicenseExpiryTo = null);
