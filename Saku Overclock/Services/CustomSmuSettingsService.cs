using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Models;

namespace Saku_Overclock.Services;
public class CustomSmuSettingsService : ICustomSmuSettingsService
{
    private const string FolderPath = "Saku Overclock/Settings";
    private const string FileName = "CustomSmuSettings.json";

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _applicationDataFolder;

    private readonly IFileService _fileService;

    public CustomSmuSettingsService(IFileService fileService)
    {
        _applicationDataFolder = Path.Combine(_localApplicationData, FolderPath);
        _fileService = fileService;
    }

    public string Note
    { 
        get; 
        set; 
    } = string.Empty;

    public List<CustomMailBoxes>? MailBoxes
    {
        get;
        set;
    }

    public List<QuickSmuCommands>? QuickSmuCommands
    {
        get;
        set;
    }

    public void LoadSettings()
    {
        var settings = _fileService.Read<CustomSmuSettingsService>(_applicationDataFolder, FileName);

        if (settings == null)
        {
            return;
        }

        foreach (var prop in typeof(CustomSmuSettingsService).GetProperties())
        {
            if (prop.CanRead && prop.CanWrite)
            {
                var value = prop.GetValue(settings);
                if (value != null)
                {
                    prop.SetValue(this, value);
                }
            }
        }
    }

    public void SaveSettings() => _fileService.Save(_applicationDataFolder, FileName, this);
}