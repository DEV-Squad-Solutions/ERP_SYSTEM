using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Logistics;

public sealed class DriverOpeningBalance : AuditableEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public int FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public DriverOpeningBalanceType BalanceType { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
