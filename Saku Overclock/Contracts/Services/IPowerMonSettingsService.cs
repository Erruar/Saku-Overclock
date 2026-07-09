namespace Saku_Overclock.Contracts.Services;

public interface IPowerMonSettingsService
{
    /// <summary>
    ///     Load PowerMon user settings
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     PowerMon user settings
    /// </summary>
    List<string> Notelist { get; }
}