using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.CashManagement;

public sealed class Cashbox : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public decimal OpeningBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CashVoucher> Vouchers { get; set; } = [];
}
