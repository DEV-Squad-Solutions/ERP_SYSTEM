using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities;

public sealed class BusinessPartner : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? TaxNumber { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal CreditLimit { get; set; }

    public bool IsActive { get; set; } = true;

}
