namespace Saku_Overclock.Contracts.Services;
public interface ITrayMenuService : IDisposable
{
    /// <summary>
    ///     Initialize application tray menu
    /// </summary>
    void Initialize();
    
    /// <summary>
    ///     Register tray commands
    /// </summary>
    /// <param name="commands">Tray commands</param>
    void RegisterCommands(ITrayCommandCollection commands);
    
    /// <summary>
    ///     Ensure tray icons created
    /// </summary>
    void EnsureTrayIconCreated();
    
    /// <summary>
    ///     Restore default tray menu (after first launch mode)
    /// </summary>
    void RestoreDefaultMenu();
    
    /// <summary>
    ///     Enter first launch mode (restricted tray)
    /// </summary>
    void SetMinimalMode();
}