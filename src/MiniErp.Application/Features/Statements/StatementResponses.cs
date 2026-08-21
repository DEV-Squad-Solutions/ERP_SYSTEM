using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record CashboxStatementItemResponse(
    int CashVoucherId,
    DateOnly Date,
    string VoucherNumber,
    string MovementName,
    string? Description,
    string? PartyName,
    decimal ReceiptAmount,
    decimal PaymentAmount,
    decimal Balance,
    string? ReferenceNumber)
{
    public CurrencyCode Currency { get; init; }

    public CurrencyCode BaseCurrency { get; init; }

    public decimal ExchangeRate { get; init; }

    public bool IsBaseCurrency { get; init; }

    public decimal BaseReceiptAmount { get; init; }

    public decimal BasePaymentAmount { get; init; }

    public decimal BaseBalance { get; init; }
}

public sealed record CashboxStatementSummaryResponse(
    decimal OpeningBalance,
    decimal TotalReceipts,
    decimal TotalPayments,
    decimal ClosingBalance)
{
    public decimal BaseOpeningBalance { get; init; }

    public decimal BaseTotalReceipts { get; init; }

    public decimal BaseTotalPayments { get; init; }

    public decimal BaseClosingBalance { get; init; }
}

public sealed record CashboxStatementResponse(
    int CashboxId,
    string CashboxName,
    CurrencyCode Currency,
    IReadOnlyList<CashboxStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    CashboxStatementSummaryResponse Summary)
{
    public CurrencyCode BaseCurrency { get; init; }

    public DateOnly OpeningBalanceDate { get; init; }

    public decimal OpeningExchangeRate { get; init; }

    public bool IsBaseCurrency { get; init; }
}

public sealed record PartnerStatementItemResponse(
    DateOnly Date,
    string DocumentNumber,
    string MovementName,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal BalanceAmount,
    string BalanceDescription,
    string? ReferenceNumber)
{
    public decimal ExchangeRate { get; init; }

    public decimal BaseDebitAmount { get; init; }

    public decimal BaseCreditAmount { get; init; }

    public decimal BaseBalanceAmount { get; init; }
}

public sealed record PartnerStatementSummaryResponse(
    decimal OpeningBalanceAmount,
    string OpeningBalanceDescription,
    decimal ClosingBalanceAmount,
    string ClosingBalanceDescription)
{
    public decimal BaseOpeningBalanceAmount { get; init; }

    public decimal BaseClosingBalanceAmount { get; init; }
}

public sealed record PartnerStatementResponse(
    int BusinessPartnerId,
    string BusinessPartnerName,
    CurrencyCode Currency,
    IReadOnlyList<PartnerStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    PartnerStatementSummaryResponse Summary)
{
    public CurrencyCode BaseCurrency { get; init; }
}

public sealed record DriverStatementItemResponse(
    int SourceId,
    DateOnly Date,
    string DocumentNumber,
    string SourceName,
    string? InvoiceNumber,
    int? DriverTripId,
    string? DriverTripNumber,
    string MovementName,
    string? Description,
    decimal AmountPaidToDriver,
    decimal AmountReceivedFromDriver,
    decimal TripCost,
    decimal BalanceAmount,
    string BalanceDescription,
    string? CashboxName,
    string? ReferenceNumber)
{
    public int? BusinessPartnerId { get; init; }

    public string? BusinessPartnerName { get; init; }

    public string? CountryName { get; init; }
}

public sealed record DriverStatementSummaryResponse(
    decimal OpeningBalanceAmount,
    string OpeningBalanceDescription,
    decimal TotalPaidToDriver,
    decimal TotalReceivedFromDriver,
    decimal TotalTripCost,
    decimal ClosingBalanceAmount,
    string ClosingBalanceDescription);

public sealed record DriverStatementResponse(
    int DriverId,
    string DriverName,
    IReadOnlyList<DriverStatementItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DriverStatementSummaryResponse Summary);

public sealed record ContainerStorePartnerResponse(
    int Id,
    string Code,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? TaxNumber,
    CurrencyCode Currency,
    bool IsActive);

public sealed record ContainerStoreHeaderResponse(
    int Id,
    string Code,
    string Name,
    string? Address,
    bool IsActive);

public sealed record ContainerStoreStatementItemResponse(
    int MovementId,
    DateOnly MovementDate,
    int InvoiceId,
    string InvoiceNumber,
    string? PartnerInvoiceNumber,
    InvoiceType InvoiceType,
    int ContainerId,
    string ContainerCode,
    string ContainerName,
    string? ContainerDescription,
    bool IsContainerActive,
    bool IsCurrentlyAssignedToStore,
    int OutgoingUnits,
    int IncomingUnits,
    int NetUnits,
    int RunningBalanceUnits,
    string? MovementDescription,
    DateTime CreatedOn);

public sealed record ContainerStoreContainerSummaryResponse(
    int ContainerId,
    string ContainerCode,
    string ContainerName,
    string? ContainerDescription,
    bool IsContainerActive,
    bool IsCurrentlyAssignedToStore,
    int OpeningUnits,
    int PeriodOutgoingUnits,
    int PeriodIncomingUnits,
    int PeriodNetUnits,
    int ClosingUnits);

public sealed record ContainerStoreStatementSummaryResponse(
    int OpeningUnits,
    int TotalOutgoingUnits,
    int TotalIncomingUnits,
    int NetUnits,
    int ClosingUnits,
    int DistinctContainerCount,
    int MovementCount);

public sealed record ContainerStoreStatementResponse(
    ContainerStorePartnerResponse BusinessPartner,
    ContainerStoreHeaderResponse ContainerStore,
    IReadOnlyList<ContainerStoreStatementItemResponse> Items,
    IReadOnlyList<ContainerStoreContainerSummaryResponse> Containers,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    ContainerStoreStatementSummaryResponse Summary);
