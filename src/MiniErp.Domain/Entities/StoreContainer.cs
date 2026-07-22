using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class StoreContainer : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ContainerId { get; set; }

    public Container Container { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
