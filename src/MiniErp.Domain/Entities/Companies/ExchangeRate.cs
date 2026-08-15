using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Companies;

public sealed class ExchangeRate : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public CurrencyCode Currency { get; set; }

    public DateOnly RateDate { get; set; }

    public decimal Rate { get; set; }

    public ExchangeRateSource Source { get; set; } = ExchangeRateSource.Manual;

    public string? Provider { get; set; }

    public string? Notes { get; set; }

    public DateTime LastModifiedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Touch(DateTime utcNow)
    {
        LastModifiedAt = utcNow;
    }
}
