namespace Saku_Overclock.Models;

public class LocalSettingsOptions
{
    /// <summary>
    ///     Application data folder where app stores config
    /// </summary>
    public string? ApplicationDataFolder
    {
        get; init;
    }

    /// <summary>
    ///     Local settings file name
    /// </summary>
    public string? LocalSettingsFile
    {
        get; init;
    }
}
