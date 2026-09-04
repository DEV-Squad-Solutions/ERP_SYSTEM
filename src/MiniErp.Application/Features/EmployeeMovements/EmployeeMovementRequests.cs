using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeMovements;

public sealed record EmployeeMovementRequest(
    int EmployeeId,
    EmployeeMovementType Type,
    decimal Amount,
    CurrencyCode Currency = CurrencyCode.EGP,
    decimal? ExchangeRate = null,
    DateOnly MovementDate = default,
    int? CashboxId = null,
    string? Notes = null)
{
    public const int NotesMaximumLength = 1_000;
}

public sealed record EmployeeMovementFilterRequest(
    int? EmployeeId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    EmployeeMovementType? Type = null,
    CurrencyCode? Currency = null,
    string? Search = null);

public sealed record BulkEmployeeMovementRequest(
    List<EmployeeMovementRequest> Movements);

public sealed record EmployeeMovementReportRequest(
    int? EmployeeId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    EmployeeMovementType? Type = null,
    CurrencyCode? Currency = null,
    string? Search = null);

