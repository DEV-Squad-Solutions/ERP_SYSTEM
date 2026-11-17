using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Containers;

public sealed class ContainerMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int FiscalYearId { get; set; }

    public MiniErp.Domain.Entities.Accounting.FiscalYear FiscalYear { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public int ContainerStoreId { get; set; }

    public Store ContainerStore { get; set; } = null!;

    public int ContainerId { get; set; }

    public Container Container { get; set; } = null!;

    /// <summary>
    /// فاتورة المصدر. null يعني حركة يدوية مباشرة على مخزن العبوات.
    /// </summary>
    public int? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// مصدر الحركة: فاتورة أو إدخال يدوي.
    /// </summary>
    public ContainerMovementSourceType SourceType { get; set; } =
        ContainerMovementSourceType.Invoice;

    /// <summary>
    /// بصمة التعديل لمنع التعارض في الحركات اليدوية.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public DateOnly MovementDate { get; set; }

    public int OutgoingUnits { get; set; }

    public int IncomingUnits { get; set; }

    public string? Description { get; set; }
}
