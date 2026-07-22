using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class InvoiceContainerLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int ContainerId { get; set; }

    public Container Container { get; set; } = null!;

    public int OutgoingUnits { get; set; }

    public int IncomingUnits { get; set; }
}
