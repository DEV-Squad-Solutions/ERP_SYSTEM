namespace MiniErp.Application.Common.Authentication;

public static class ApplicationRoles
{
    public const string Admin = "Admin";

    public const string User = "User";

    public const string Accountant = "Accountant";

    public const string Cashier = "Cashier";

    public const string Authenticated =
        Admin + "," + User + "," + Accountant + "," + Cashier;

    public static readonly IReadOnlyList<string> All =
    [Admin, User, Accountant, Cashier];
}
