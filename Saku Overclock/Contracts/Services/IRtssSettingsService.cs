using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Saku_Overclock.Services;

namespace Saku_Overclock.Contracts.Services;
internal interface IRtssSettingsService
{
    void SaveSettings();
    void LoadSettings();

    List<RTSSElementsClass> RTSS_Elements
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
