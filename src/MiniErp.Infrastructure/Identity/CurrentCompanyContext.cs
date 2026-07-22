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
            "لا يمكن تحديد الشركة الحالية. يجب تنفيذ خدمات الشركات داخل طلب " +
            "مسجل الدخول ويحتوي على قيمة company_id واحدة وصحيحة.");
    }
}
