using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.BusinessPartners;

public sealed class PartnerOpeningBalance : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int BusinessPartnerId { get; set; }

    public BusinessPartner BusinessPartner { get; set; } = null!;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.EGP;

    public PartnerBalanceType BalanceType { get; set; }

    public decimal Amount { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? Notes { get; set; }

}
