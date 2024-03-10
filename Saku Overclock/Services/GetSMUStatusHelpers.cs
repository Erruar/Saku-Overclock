using Saku_Overclock.Services;

internal static class GetSMUStatusHelpers
{

    public static string GetByType(SMU.Status type)
    {
        string str;
        return GetSMUStatus.status.TryGetValue(type, out str) ? str : "Unknown Status";
    }
}