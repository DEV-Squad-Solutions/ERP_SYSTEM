namespace MiniErp.Application.Features.Users;

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    IReadOnlyList<UserCompanyResponse> Companies);
