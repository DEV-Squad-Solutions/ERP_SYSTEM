using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public interface IOpeningBalancePostingService
{
    Task<Result> SynchronizeCashboxAsync(
        int cashboxId,
        CancellationToken cancellationToken = default);

    Task<Result> SynchronizePartnerAsync(
        int openingBalanceId,
        CancellationToken cancellationToken = default);

    Task<Result> SynchronizeEmployeeAsync(
        int openingBalanceId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        JournalEntrySourceType sourceType,
        int sourceId,
        CancellationToken cancellationToken = default);
}
