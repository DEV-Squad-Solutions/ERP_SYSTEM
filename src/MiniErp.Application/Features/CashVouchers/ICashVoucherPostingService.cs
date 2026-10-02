using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Application.Features.CashVouchers;

public interface ICashVoucherPostingService
{
    Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        CancellationToken cancellationToken = default);

    // Updates save the voucher before posting. Carry reference changes across
    // that save, which resets EF's original values.
    Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        bool partnerReferencesChanged,
        CancellationToken cancellationToken = default) =>
        SynchronizeAsync(voucher, cancellationToken);

    Task<Result> DeleteAsync(
        int voucherId,
        CancellationToken cancellationToken = default);
}
