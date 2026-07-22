using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Domain.Entities.Containers;

public sealed class ContainerMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int ContainerStoreId { get; set; }

    public Store ContainerStore { get; set; } = null!;

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
