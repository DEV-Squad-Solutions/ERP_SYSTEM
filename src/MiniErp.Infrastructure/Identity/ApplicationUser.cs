using Microsoft.AspNetCore.Identity;

namespace MiniErp.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string ProfileImage { get; set; } = string.Empty;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<UserCompany> UserCompanies { get; set; } = [];
}
