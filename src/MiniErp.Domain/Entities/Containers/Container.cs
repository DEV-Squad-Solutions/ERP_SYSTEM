using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Containers;

public sealed class Container : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<StoreContainer> StoreContainers { get; set; } = [];
}
