namespace MiniErp.Application.Features.Users;

public sealed record UserFilterRequest(
    string? Search = null,
    string? UserName = null,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null);
