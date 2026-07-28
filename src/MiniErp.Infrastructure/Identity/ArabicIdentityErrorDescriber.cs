using Microsoft.AspNetCore.Identity;

namespace MiniErp.Infrastructure.Identity;

public sealed class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Create(nameof(DefaultError), "حدث خطأ غير معروف.");

    public override IdentityError ConcurrencyFailure() =>
        Create(nameof(ConcurrencyFailure), "تم تعديل البيانات بواسطة عملية أخرى؛ أعد المحاولة.");

    public override IdentityError PasswordMismatch() =>
        Create(nameof(PasswordMismatch), "كلمة المرور غير صحيحة.");

    public override IdentityError InvalidToken() =>
        Create(nameof(InvalidToken), "الرمز غير صالح.");

    public override IdentityError LoginAlreadyAssociated() =>
        Create(nameof(LoginAlreadyAssociated), "بيانات تسجيل الدخول مرتبطة بحساب آخر.");

    public override IdentityError InvalidUserName(string? userName) =>
        Create(nameof(InvalidUserName), $"اسم المستخدم '{userName}' غير صالح.");

    public override IdentityError InvalidEmail(string? email) =>
        Create(nameof(InvalidEmail), $"البريد الإلكتروني '{email}' غير صالح.");

    public override IdentityError DuplicateUserName(string userName) =>
        Create(nameof(DuplicateUserName), $"اسم المستخدم '{userName}' مستخدم بالفعل.");

    public override IdentityError DuplicateEmail(string email) =>
        Create(nameof(DuplicateEmail), $"البريد الإلكتروني '{email}' مستخدم بالفعل.");

    public override IdentityError InvalidRoleName(string? role) =>
        Create(nameof(InvalidRoleName), $"اسم الدور '{role}' غير صالح.");

    public override IdentityError DuplicateRoleName(string role) =>
        Create(nameof(DuplicateRoleName), $"اسم الدور '{role}' مستخدم بالفعل.");

    public override IdentityError UserAlreadyHasPassword() =>
        Create(nameof(UserAlreadyHasPassword), "لدى المستخدم كلمة مرور بالفعل.");

    public override IdentityError UserLockoutNotEnabled() =>
        Create(nameof(UserLockoutNotEnabled), "خاصية قفل المستخدم غير مفعلة.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Create(nameof(UserAlreadyInRole), $"المستخدم لديه الدور '{role}' بالفعل.");

    public override IdentityError UserNotInRole(string role) =>
        Create(nameof(UserNotInRole), $"المستخدم لا يملك الدور '{role}'.");

    public override IdentityError PasswordTooShort(int length) =>
        Create(nameof(PasswordTooShort), $"يجب ألا تقل كلمة المرور عن {length} أحرف.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Create(nameof(PasswordRequiresNonAlphanumeric), "يجب أن تحتوي كلمة المرور على رمز خاص واحد على الأقل.");

    public override IdentityError PasswordRequiresDigit() =>
        Create(nameof(PasswordRequiresDigit), "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل.");

    public override IdentityError PasswordRequiresLower() =>
        Create(nameof(PasswordRequiresLower), "يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير واحد على الأقل.");

    public override IdentityError PasswordRequiresUpper() =>
        Create(nameof(PasswordRequiresUpper), "يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير واحد على الأقل.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Create(nameof(PasswordRequiresUniqueChars), $"يجب أن تحتوي كلمة المرور على {uniqueChars} أحرف مختلفة على الأقل.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Create(nameof(RecoveryCodeRedemptionFailed), "رمز الاسترداد غير صالح.");

    private static IdentityError Create(string code, string description) =>
        new()
        {
            Code = code,
            Description = description
        };
}
