using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Domain.Entities.Companies;

public sealed class Company : AuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string CommercialRegister { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public string ManagerName { get; set; } = string.Empty;

    public byte[] RowVersion { get; private set; } = [];

    public CompanySettings? Settings { get; set; }

    public ICollection<FiscalYear> FiscalYears { get; set; } = [];
}
