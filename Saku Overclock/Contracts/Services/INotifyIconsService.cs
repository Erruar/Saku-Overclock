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
    ///     Request for updating icons
    /// </summary>
    void UpdateTrayMonIcons();
}