using System.Globalization;
using Microsoft.AspNetCore.Http;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Identity;

public sealed class CurrentCompanyService(IHttpContextAccessor httpContextAccessor)
    : ICurrentCompanyService, IScopedService
{
    public Result<int> GetCompanyId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return Result<int>.Failure(
                Error.Unauthorized(
                    "CompanyContext.Unauthenticated",
                    "An authenticated user is required to select a company."));
        }

        var allowedCompanyIds = httpContext.User
            .FindAll(CustomClaimTypes.CompanyId)
            .Select(claim => int.TryParse(
                claim.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var companyId)
                    ? companyId
                    : 0)
            .Where(companyId => companyId > 0)
            .ToHashSet();

        if (allowedCompanyIds.Count == 0)
        {
            return Result<int>.Failure(
                Error.Forbidden(
                    "CompanyContext.NoAccess",
                    "The logged-in user is not assigned to a company."));
        }

        var headerValue = httpContext.Request.Headers[CustomClaimTypes.CompanyHeader]
            .ToString()
            .Trim();

        if (string.IsNullOrEmpty(headerValue))
        {
            return allowedCompanyIds.Count == 1
                ? Result<int>.Success(allowedCompanyIds.Single())
                : Result<int>.Failure(
                    Error.Validation(
                        "CompanyContext.Required",
                        $"Header '{CustomClaimTypes.CompanyHeader}' is required when the user has access to more than one company."));
        }

        if (!int.TryParse(
                headerValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var selectedCompanyId) ||
            selectedCompanyId <= 0)
        {
            return Result<int>.Failure(
                Error.Validation(
                    "CompanyContext.Invalid",
                    $"Header '{CustomClaimTypes.CompanyHeader}' must contain a positive integer company ID."));
        }

        return allowedCompanyIds.Contains(selectedCompanyId)
            ? Result<int>.Success(selectedCompanyId)
            : Result<int>.Failure(
                Error.Forbidden(
                    "CompanyContext.Forbidden",
                    "The logged-in user does not have access to the selected company."));
    }
}
