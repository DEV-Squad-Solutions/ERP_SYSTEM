using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashboxTransfers;

public sealed record CashboxTransferResponse(
    int Id,
    int CompanyId,
    string TransferNumber,
    DateOnly TransferDate,
    int SourceCashboxId,
    string SourceCashboxName,
    int DestinationCashboxId,
    string DestinationCashboxName,
    decimal Amount,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    decimal BaseAmount,
    int PaymentVoucherId,
    string PaymentVoucherNumber,
    int ReceiptVoucherId,
    string ReceiptVoucherNumber,
    string? Description,
    string? Notes,
    DateTime LastModifiedAt,
    byte[] RowVersion);

public sealed record CashboxTransferListResponse(
    int Id,
    int CompanyId,
    string TransferNumber,
    DateOnly TransferDate,
    int SourceCashboxId,
    string SourceCashboxName,
    int DestinationCashboxId,
    string DestinationCashboxName,
    decimal Amount,
    CurrencyCode Currency,
    string? Description,
    DateTime LastModifiedAt,
    byte[] RowVersion);
