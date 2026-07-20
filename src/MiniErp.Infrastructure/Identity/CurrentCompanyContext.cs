using Microsoft.AspNetCore.Http;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;

namespace MiniErp.Infrastructure.Identity;

public sealed class CurrentCompanyContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentCompanyContext, IScopedService
{
    private int? companyId;

    public int CompanyId => companyId ??= ResolveCompanyId();

    private int ResolveCompanyId()
    {
        if (CompanyClaimResolver.TryGetCompanyId(
                httpContextAccessor.HttpContext?.User,
                out var resolvedCompanyId))
        {
            return resolvedCompanyId;
        }

        throw new InvalidOperationException(
            "The current company context is unavailable. Tenant services must run inside an authenticated request with exactly one valid company_id claim.");
    }
}
