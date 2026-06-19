using System.Text.Json.Serialization;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;

namespace Saku_Overclock.Services;

public class PowerMonSettingsService : IPowerMonSettingsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "PowerMon.json";

    private readonly string _applicationDataFolder = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderPath);

    private readonly IFileService? _fileService;
    
    [JsonConstructor]
    private PowerMonSettingsService() { }

    public PowerMonSettingsService(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    public List<string> Notelist
    {
        get;
        set;
    } = [];
    
    public void LoadSettings()
    {
        try
        {
            Notelist = _fileService?.Read<List<string>>(_applicationDataFolder, FileName) ?? [];
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }

    public void SaveSettings() => _fileService?.Save(_applicationDataFolder, FileName, Notelist);
}