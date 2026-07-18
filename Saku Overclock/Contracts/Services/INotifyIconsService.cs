using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Contracts.Services;

public interface INotifyIconsService
{
    /// <summary>
    ///     Loading TrayMon settings
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     Saving TrayMon settings
    /// </summary>
    void SaveSettings();
    
    /// <summary>
    ///     TrayMon elements
    /// </summary>
    public List<NiIconsElements> Elements
    {
        get;
        set;
    }

    /// <summary>
    ///     Creating all enabled tray icons
    /// </summary>
    public void CreateNotifyIcons();
    
    /// <summary>
    ///     Update icons appearance (use from pages)
    /// </summary>
    void UpdateTrayMonIcons();

    /// <summary>
    ///     Update icons data
    /// </summary>
    /// <param name="sensorsInformation">Sensors data</param>
    public void UpdateNotifyIcons(SensorsInformation sensorsInformation);

    /// <summary>
    ///     Destroy all active icons
    /// </summary>
    public void DisposeAllNotifyIcons();

    /// <summary>
    ///     Are icons created flag
    /// </summary>
    public bool IsIconsCreated
    {
        get; 
        set;
    }
    
    
    /// <summary>
    ///     Are icons updated flag
    /// </summary>
    public bool IsIconsUpdated
    {
        get;
        set;
    }
}