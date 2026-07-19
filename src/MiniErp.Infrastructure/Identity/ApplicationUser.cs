using Microsoft.AspNetCore.Identity;

namespace MiniErp.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string ProfileImage { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
