using Microsoft.AspNetCore.Http;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService, IScopedService
{
    public Result<Guid> GetUserId()
    {
        var userIdValue = httpContextAccessor.HttpContext?.User
            .FindFirst("sub")?
            .Value;

        return Guid.TryParse(userIdValue, out var userId) && userId != Guid.Empty
            ? Result<Guid>.Success(userId)
            : Result<Guid>.Failure(
                Error.Unauthorized(
                    "Authentication.InvalidUserContext",
                    "رقم المستخدم المسجل دخوله غير موجود أو غير صالح."));
    }
}
