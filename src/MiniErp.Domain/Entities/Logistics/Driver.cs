using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Logistics;

public sealed class Driver : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? NationalId { get; set; }

    public string LicenseNumber { get; set; } = string.Empty;

    public DateOnly? LicenseExpiryDate { get; set; }

    public bool IsInternal { get; set; }

    public bool IsActive { get; set; } = true;

}
