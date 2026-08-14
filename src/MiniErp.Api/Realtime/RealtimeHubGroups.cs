namespace MiniErp.Api.Realtime;

internal static class RealtimeHubGroups
{
    public static string Company(int companyId) => $"company:{companyId}";

    public static string CompanyRole(int companyId, string role) =>
        $"company:{companyId}:role:{role}";
}
