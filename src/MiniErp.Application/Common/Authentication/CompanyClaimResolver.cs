using System.Globalization;
using System.Security.Claims;

namespace MiniErp.Application.Common.Authentication;

public static class CompanyClaimResolver
{
    public static bool TryGetCompanyId(
        ClaimsPrincipal? principal,
        out int companyId)
    {
        companyId = 0;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var claims = principal.FindAll(CustomClaimTypes.CompanyId).ToArray();
        return claims.Length == 1 &&
            int.TryParse(
                claims[0].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out companyId) &&
            companyId > 0;
    }
}
