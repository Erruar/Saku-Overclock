namespace Saku_Overclock.Contracts.Services;

public interface IPowerMonSettingsService
{
    /// <summary>
    ///     Load PowerMon user settings
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     Save PowerMon user settings
    /// </summary>
    void SaveSettings();
    
    /// <summary>
    ///     PowerMon user settings
    /// </summary>
    List<string> Notelist { get; }
}