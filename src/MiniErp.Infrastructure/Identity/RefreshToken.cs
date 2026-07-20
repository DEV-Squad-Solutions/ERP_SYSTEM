using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int? CompanyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser User { get; set; } = null!;

    public Company? Company { get; set; }
}
