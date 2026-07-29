using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities.Companies;

public sealed class Company : AuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string CommercialRegister { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public string ManagerName { get; set; } = string.Empty;

    public CompanySettings? Settings { get; set; }
}
