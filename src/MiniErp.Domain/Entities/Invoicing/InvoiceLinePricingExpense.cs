using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Invoicing;

public sealed class InvoiceLinePricingExpense : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int InvoiceLineId { get; set; }

    public InvoiceLine InvoiceLine { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Notes { get; set; }
}
