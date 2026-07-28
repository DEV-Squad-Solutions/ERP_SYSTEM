using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class Store : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int? BusinessPartnerId { get; set; }

    public BusinessPartner? BusinessPartner { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsContainerStore { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<StoreContainer> StoreContainers { get; set; } = [];
}
