using Saku_Overclock.Services;

namespace Saku_Overclock.Contracts.Services;
internal interface IRtssSettingsService
{
    void SaveSettings();
    void LoadSettings();

    List<RtssElementsClass> RtssElements
    {
        get; set;
    }
    
    bool IsAdvancedCodeEditorEnabled
    {
        get; set;
    }
    
    string AdvancedCodeEditor
    { 
        get; set; 
    }
    
}
