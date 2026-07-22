using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Infrastructure.Identity;

public sealed class UserCompany
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;
}
