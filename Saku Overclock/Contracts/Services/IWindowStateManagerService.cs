namespace Saku_Overclock.Contracts.Services;
public interface IWindowStateManagerService
{
    /// <summary>
    ///     Initialize window state manager
    /// </summary>
    void Initialize();
    
    /// <summary>
    ///     Toggle window visibility
    /// </summary>
    void ToggleWindowVisibility();
    
    /// <summary>
    ///     Show main window
    /// </summary>
    void ShowMainWindow();
    
    /// <summary>
    ///     Set window title bar options
    /// </summary>
    /// <param name="scaleAdjustment">App scale</param>
    /// <returns>Title bar bounds</returns>
    (double, double) SetWindowTitleBarBounds(double scaleAdjustment);
}