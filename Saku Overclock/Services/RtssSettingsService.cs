using System.Buffers;
using System.Text.RegularExpressions;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Wrappers;

namespace Saku_Overclock.Services;

public partial class RtssSettingsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<RtssSettings>(ipc, "RtssSettings", IpcJsonContext.Default.RtssSettings, new())
        , IRtssSettingsService
{
    public List<RtssElementsClass> RtssElements
    {
        get => Get(s => s.RtssElements.ToList());
        set => Set(cache => cache.RtssElements = value);
    }

    public bool IsAdvancedCodeEditorEnabled
    {
        get => Get(s => s.IsAdvancedCodeEditorEnabled);
        set => Set(cache => cache.IsAdvancedCodeEditorEnabled = value);
    }

    public string AdvancedCodeEditor
    {
        get => Get(s => s.AdvancedCodeEditor);
        set => Set(cache => cache.AdvancedCodeEditor = value);
    }

    public void LoadSettings() => _ = LoadSettingsAsync();
    public void SaveSettings() => Save();

    public bool IsRtssUpdated { get; set; }
    public void UpdateRtssMetrics(SensorsInformation sensorsInformation, string? appliedPreset, int? coreCount)
    {
        throw new NotImplementedException();
    }
}