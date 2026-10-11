using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Users;

public static class CashboxPermissionErrors
{
    public static Error CompanyAccessDenied() => Error.Forbidden("CashboxPermissions.CompanyAccessDenied", "ليس لديك صلاحية الوصول إلى هذه الشركة.");
    public static Error ViewDenied() => Error.Forbidden("CashboxPermissions.ViewDenied", "ليس لديك صلاحية عرض هذه الخزنة.");
    public static Error ExecuteDenied() => Error.Forbidden("CashboxPermissions.ExecuteDenied", "ليس لديك صلاحية تنفيذ عمليات على هذه الخزنة.");
    public static Error AdminRequired() => Error.Forbidden("CashboxPermissions.AdminRequired", "إدارة صلاحيات الخزن متاحة للمسؤول فقط.");
    public static Error UserNotInCompany() => Error.NotFound("CashboxPermissions.UserNotInCompany", "المستخدم غير مسند إلى الشركة الحالية.");
    public static Error InvalidCashboxes() => Error.Validation("CashboxPermissions.InvalidCashboxes", "يجب أن تكون الخزن من الشركة الحالية وأن تكون الخزن الجديدة نشطة.", "Permissions");
    public static Error InvalidRequest() => Error.Validation("CashboxPermissions.InvalidRequest", "بيانات صلاحيات الخزن غير صحيحة.", "Permissions");
    public static Error Conflict() => Error.Conflict("CashboxPermissions.Conflict", "تم تغيير الصلاحيات. أعد تحميلها قبل الحفظ.", "Revision");
}
