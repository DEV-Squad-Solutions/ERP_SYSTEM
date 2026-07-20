using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Identity;

public sealed class UserCompany
{
    public Guid UserId { get; set; }

    public int CompanyId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Company Company { get; set; } = null!;
}
