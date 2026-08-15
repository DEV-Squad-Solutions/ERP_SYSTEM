using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Authentication;

public static class AuthenticationErrors
{
    public static Error CompanyAccessDenied() =>
        Error.Forbidden(
            "Authentication.CompanyAccessDenied",
            "لا يملك المستخدم صلاحية الوصول إلى الشركة المحددة.");

    public static Error InvalidCredentialsError() =>
        Error.Unauthorized(
            "Authentication.InvalidCredentials",
            "اسم المستخدم أو كلمة المرور غير صحيحة.");

    public static Error InvalidRefreshTokenError() =>
        Error.Unauthorized(
            "Authentication.InvalidRefreshToken",
            "رمز التحديث غير صالح أو منتهي الصلاحية.");

    public static Error InvalidUserContext() =>
        Error.Unauthorized(
            "Authentication.InvalidUserContext",
            "رقم المستخدم المسجل دخوله غير موجود أو غير صالح.");

    public static Error InvalidCompanySelectionTokenError() =>
        Error.Unauthorized(
            "Authentication.InvalidCompanySelectionToken",
            "رمز اختيار الشركة غير صالح أو منتهي الصلاحية.");

    public static Error NoCompanyAccess() =>
        Error.Forbidden(
            "Authentication.NoCompanyAccess",
            "المستخدم غير مرتبط بأي شركة نشطة.");
}
