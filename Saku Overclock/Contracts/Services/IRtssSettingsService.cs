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
    
    /// <summary>
    ///     Is rtss overlay updated
    /// </summary>
    public bool IsRtssUpdated
    {
        get; 
        set;
    }

    /// <summary>
    ///     Update rtss overlay metrics
    /// </summary>
    /// <param name="sensorsInformation">Sensors data</param>
    /// <param name="appliedPreset">Selected preset</param>
    /// <param name="coreCount">Core count</param>
    public void UpdateRtssMetrics(SensorsInformation sensorsInformation, string? appliedPreset, int? coreCount);
}