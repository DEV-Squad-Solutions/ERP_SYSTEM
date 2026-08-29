using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record CashboxStatementFilterRequest(
    int CashboxId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CashDirection? Direction = null,
    int? CashMovementTypeId = null,
    CashMovementClassification? Classification = null,
    CashPartyType? PartyType = null,
    int? BusinessPartnerId = null,
    int? DriverId = null,
    int? DriverTripId = null,
    string? VoucherNumber = null,
    int? EmployeeId = null);

public sealed record PartnerStatementFilterRequest(
    int BusinessPartnerId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    PartnerStatementSourceType? SourceType = null,
    BusinessPartnerMovementType? MovementType = null,
    int? CashMovementTypeId = null,
    CashMovementClassification? Classification = null);

public sealed record DriverStatementFilterRequest(
    int DriverId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CashDirection? Direction = null,
    int? CashMovementTypeId = null,
    CashMovementClassification? Classification = null,
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

public sealed record EmployeeStatementFilterRequest(
    int EmployeeId,
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    EmployeeStatementSourceType? SourceType = null,
    EmployeeMovementType? MovementType = null);

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

public enum EmployeeStatementSourceType
{
    OpeningBalance = 1,
    SalaryTransfer = 2,
    Movement = 3,
    CashVoucher = 4
}
