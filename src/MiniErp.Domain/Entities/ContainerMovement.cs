using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class ContainerMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ContainerId { get; set; }

    public Container Container { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateOnly MovementDate { get; set; }

    public int OutgoingUnits { get; set; }

    public int IncomingUnits { get; set; }

    public string? Description { get; set; }
}
