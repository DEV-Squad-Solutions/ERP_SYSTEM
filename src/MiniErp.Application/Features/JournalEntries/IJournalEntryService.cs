using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.JournalEntries;

public interface IJournalEntryService
{
    Task<Result<PagedResponse<JournalEntryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        JournalEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<JournalEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<JournalEntryResponse>> AddAsync(
        JournalEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JournalEntryResponse>> ReverseAsync(
        int id,
        JournalEntryReverseRequest request,
        CancellationToken cancellationToken = default);
}
