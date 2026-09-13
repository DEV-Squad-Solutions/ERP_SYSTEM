using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Tests.TestDoubles;

internal sealed class NoOpCashVoucherPostingService : ICashVoucherPostingService
{
    public List<int> SynchronizedVoucherIds { get; } = [];

    public List<int> DeletedVoucherIds { get; } = [];

    public bool FailSynchronization { get; set; }

    public Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        CancellationToken cancellationToken = default)
    {
        SynchronizedVoucherIds.Add(voucher.Id);
        if (FailSynchronization)
        {
            return Task.FromResult(
                Result<AutomaticJournalEntryResult>.Failure(
                    Error.Validation(
                        "Tests.CashVoucherPostingFailed",
                        "Posting test failure.")));
        }

        return Task.FromResult(
            Result<AutomaticJournalEntryResult>.Success(
                new AutomaticJournalEntryResult(
                    JournalEntryId: 0,
                    EntryNumber: $"TEST-{voucher.Id}",
                    Created: false)));
    }

    public Task<Result> DeleteAsync(
        int voucherId,
        CancellationToken cancellationToken = default)
    {
        DeletedVoucherIds.Add(voucherId);
        return Task.FromResult(Result.Success());
    }
}
