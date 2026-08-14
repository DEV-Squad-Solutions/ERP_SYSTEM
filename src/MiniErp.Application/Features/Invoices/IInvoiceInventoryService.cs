using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public interface IInvoiceInventoryService
{
    Task LockCostingAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default);

    Task<Error?> RecalculateCostingAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default);

    Task<Error?> ValidateStockAsync(
        int storeId,
        DateOnly invoiceDate,
        InvoiceType invoiceType,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? currentInvoiceId,
        string? currentInvoiceNumber,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceItemBalanceResponse>> GetItemBalanceAsync(
        int storeId,
        int itemId,
        DateOnly asOfDate,
        int? invoiceId = null,
        CancellationToken cancellationToken = default);
}
