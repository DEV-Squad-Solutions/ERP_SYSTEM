namespace MiniErp.Application.Features.Users;

public sealed record UserUpdateRequest(
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<int> CompanyIds);
