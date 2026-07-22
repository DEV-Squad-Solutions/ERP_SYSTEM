using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class DriverTrip : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public int InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public string InvoiceNumber { get; set; } = string.Empty;

    public string? ExportInvoiceCode { get; set; }

    public DateOnly TripDate { get; set; }

    public decimal? Price { get; set; }

}
