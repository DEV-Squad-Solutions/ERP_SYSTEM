using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface ICurrentUserService
{
    Result<Guid> GetUserId();
}
