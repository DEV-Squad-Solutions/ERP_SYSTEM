using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Users;

public static class UserErrors
{
    public static Error CannotDeleteCurrentUser() =>
        Error.Conflict(
            "Users.CannotDeleteCurrentUser",
            "لا يمكن للمستخدم الحالي حذف حسابه بنفسه.");

    public static Error UserNameExists(string userName) =>
        Error.Conflict(
            "Users.UserNameExists",
            $"اسم المستخدم '{userName}' مستخدم بالفعل.",
            nameof(UserCreateRequest.UserName));

    public static Error UserNameExistsFromIdentity(string description) =>
        Error.Conflict(
            "Users.UserNameExists",
            description,
            nameof(UserCreateRequest.UserName));

    public static Error EmailExists(string email) =>
        Error.Conflict(
            "Users.EmailExists",
            $"البريد الإلكتروني '{email}' مستخدم بالفعل.",
            nameof(UserCreateRequest.Email));

    public static Error EmailExistsFromIdentity(string description) =>
        Error.Conflict(
            "Users.EmailExists",
            description,
            nameof(UserCreateRequest.Email));

    public static Error RolesNotFound(IEnumerable<string> roles) =>
        Error.NotFound(
            "Users.RolesNotFound",
            $"لم يتم العثور على الأدوار التالية: {string.Join(", ", roles)}.");

    public static Error CompaniesNotFound(IEnumerable<int> companyIds) =>
        Error.NotFound(
            "Users.CompaniesNotFound",
            $"الشركات التالية غير موجودة أو محذوفة: {string.Join(", ", companyIds)}.");

    public static Error IdentityValidation(IEnumerable<string> descriptions) =>
        Error.Validation(
            "Users.IdentityValidation",
            string.Join("; ", descriptions));

    public static Error InvalidId() =>
        Error.Validation("Users.InvalidId", "رقم المستخدم مطلوب.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Users.NotFound", $"لم يتم العثور على المستخدم رقم {id}.");

    public static Error LastAdminError() =>
        Error.Conflict(
            "Users.LastAdmin",
            "لا يمكن حذف آخر مستخدم مسؤول أو إزالة دور المسؤول من حسابه.");
}
