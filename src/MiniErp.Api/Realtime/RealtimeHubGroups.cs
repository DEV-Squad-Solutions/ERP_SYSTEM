namespace MiniErp.Api.Realtime;

internal static class RealtimeHubGroups
{
    public static string Company(int companyId) => $"company:{companyId}";
}
