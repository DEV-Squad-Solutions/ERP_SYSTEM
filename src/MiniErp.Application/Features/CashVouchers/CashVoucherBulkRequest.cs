using System.Text.Json.Serialization;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public enum CashVoucherBulkAction
{
    Add = 1,
    Update = 2,
    Delete = 3
}

public sealed record CashVoucherBulkRequest(
    IReadOnlyList<CashVoucherBulkItemRequest>? Items);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(CashVoucherBulkAddItemRequest), "Add")]
[JsonDerivedType(typeof(CashVoucherBulkUpdateItemRequest), "Update")]
[JsonDerivedType(typeof(CashVoucherBulkDeleteItemRequest), "Delete")]
public abstract record CashVoucherBulkItemRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CashVoucherBulkAddItemRequest(
    [property: JsonRequired] CashVoucherBulkVoucherRequest? Voucher)
    : CashVoucherBulkItemRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CashVoucherBulkUpdateItemRequest(
    [property: JsonRequired] int Id,
    [property: JsonRequired] byte[]? RowVersion,
    [property: JsonRequired] CashVoucherBulkVoucherRequest? Voucher)
    : CashVoucherBulkItemRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CashVoucherBulkDeleteItemRequest(
    [property: JsonRequired] int Id,
    [property: JsonRequired] byte[]? RowVersion)
    : CashVoucherBulkItemRequest;

public sealed record CashVoucherBulkVoucherRequest(
    DateOnly VoucherDate,
    CashDirection Direction,
    int CashboxId,
    int CashMovementTypeId,
    int? EmployeeId,
    int? BusinessPartnerId,
    int? DriverId,
    int? DriverTripId,
    string? ExternalPartyName,
    decimal Amount,
    string? ReferenceNumber,
    string? Description,
    string? Notes,
    decimal? ExchangeRate);

public sealed record CashVoucherBulkResponse(
    IReadOnlyList<CashVoucherBulkItemResponse> Items,
    CashVoucherBulkSummary Summary);

public sealed record CashVoucherBulkItemResponse(
    CashVoucherBulkAction Action,
    string Status,
    int Id,
    CashVoucherResponse? Voucher);

public sealed record CashVoucherBulkSummary(
    int Added,
    int Updated,
    int Deleted);
