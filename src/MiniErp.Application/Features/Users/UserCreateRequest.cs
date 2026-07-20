namespace MiniErp.Application.Features.Users;

public sealed record UserCreateRequest(
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Password,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<int> CompanyIds);
