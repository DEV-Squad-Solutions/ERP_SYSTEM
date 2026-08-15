using System.Globalization;
using System.Linq.Expressions;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;

namespace MiniErp.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private RealtimeJobRequest CreateRealtimeRequest(
        Guid operationId,
        string action,
        object entityId,
        int? companyId = null)
    {
        var resolvedCompanyId = companyId;
        if (!resolvedCompanyId.HasValue &&
            CompanyClaimResolver.TryGetCompanyId(User, out var claimCompanyId))
        {
            resolvedCompanyId = claimCompanyId;
        }

        if (!resolvedCompanyId.HasValue || resolvedCompanyId.Value <= 0)
        {
            throw new InvalidOperationException(
                "A valid company is required for realtime targeting.");
        }

        Guid? actorUserId = null;
        if (Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) &&
            userId != Guid.Empty)
        {
            actorUserId = userId;
        }

        return new RealtimeJobRequest(
            OperationId: operationId,
            Action: action,
            EntityId: Convert.ToString(
                entityId,
                CultureInfo.InvariantCulture) ?? string.Empty,
            ActorUserId: actorUserId,
            CompanyId: resolvedCompanyId.Value);
    }

    protected void TryEnqueueRealtime<TJob>(
        string action,
        object entityId,
        Func<RealtimeJobRequest, Expression<Func<TJob, Task>>> jobCallFactory,
        int? companyId = null,
        Guid? operationId = null)
    {
        var resolvedOperationId = operationId ?? Guid.NewGuid();
        try
        {
            var request = CreateRealtimeRequest(
                resolvedOperationId,
                action,
                entityId,
                companyId);
            HttpContext.RequestServices
                .GetRequiredService<IBackgroundJobClient>()
                .Enqueue(jobCallFactory(request));
        }
        catch (Exception exception)
        {
            try
            {
                ControllerContext.HttpContext?
                    .RequestServices
                    .GetService<ILoggerFactory>()?
                    .CreateLogger(GetType())
                    .LogWarning(
                        exception,
                        "Realtime job for operation {OperationId} could not be enqueued after the business operation was saved.",
                        resolvedOperationId);
            }
            catch
            {
                // Logging must not turn an already committed CRUD operation
                // into an API failure.
            }
        }
    }
}
