using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Application.Features.CashVouchers;

public interface ICashVoucherPostingService
{
    Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int voucherId,
        CancellationToken cancellationToken = default);
}
