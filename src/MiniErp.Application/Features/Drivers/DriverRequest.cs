namespace MiniErp.Application.Features.Drivers;

public sealed record DriverRequest(
    string Name,
    string? PhoneNumber,
    string? NationalId,
    string LicenseNumber,
    DateOnly? LicenseExpiryDate,
    bool IsActive = true);
