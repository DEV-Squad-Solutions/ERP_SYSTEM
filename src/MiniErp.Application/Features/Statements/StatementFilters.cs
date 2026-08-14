using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record CashboxStatementFilterRequest(
    int CashboxId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CashDirection? Direction = null,
    int? CashMovementTypeId = null,
    CashPartyType? PartyType = null,
    int? BusinessPartnerId = null,
    int? DriverId = null,
    int? DriverTripId = null,
    string? VoucherNumber = null);

public sealed record PartnerStatementFilterRequest(
    int BusinessPartnerId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    PartnerStatementSourceType? SourceType = null,
    BusinessPartnerMovementType? MovementType = null,
    int? CashMovementTypeId = null);

public sealed record DriverStatementFilterRequest(
    int DriverId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CashDirection? Direction = null,
    int? CashMovementTypeId = null,
    int? DriverTripId = null,
    string? InvoiceNumber = null,
    bool? TransactionsWithoutTrip = null,
    bool? HasCost = null);

public sealed record ContainerStoreStatementFilterRequest(
    int BusinessPartnerId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? ContainerId = null,
    InvoiceType? InvoiceType = null,
    string? InvoiceNumber = null,
    ContainerMovementDirection? Direction = null);

public enum ContainerMovementDirection
{
    Outgoing = 1,
    Incoming = 2
}

public enum PartnerStatementSourceType
{
    OpeningBalance = 1,
    Invoice = 2,
    CashVoucher = 3
}

public enum DriverStatementSourceType
{
    CashVoucher = 1,
    DriverTrip = 2
}
