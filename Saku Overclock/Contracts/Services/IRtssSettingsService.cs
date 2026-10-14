using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IRtssSettingsService
{
    /// <summary>
    ///     Load rtss overlay settings
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     Save rtss overlay settings
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Rtss elements settings
    /// </summary>
    List<RtssElementsClass> RtssElements
    {
        get;
    }

    /// <summary>
    ///     Rtss advanced code editor enabled flag
    /// </summary>
    bool IsAdvancedCodeEditorEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Rtss code line (used to draw overlay)
    /// </summary>
    string AdvancedCodeEditor
    {
        get;
        set;
    }
}