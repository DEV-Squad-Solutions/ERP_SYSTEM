using MiniErp.Application.Common.Realtime;

namespace MiniErp.Application.Common.Abstractions;

public interface ICashboxRealtimeAudience
{
    Task<Guid[]> ResolveAsync(string resource, RealtimeJobRequest request, CancellationToken cancellationToken = default);
}
