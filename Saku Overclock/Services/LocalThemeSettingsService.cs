using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Models;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Services;

public class LocalThemeSettingsService : ILocalThemeSettingsService
{
    private const string DefaultApplicationDataFolder = "Saku Overclock/Settings/Themes";
    private const string ThemeSettingsFile = "ThemeSettings.json";

    private readonly IFileService _fileService;

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _applicationDataFolder;
    private readonly string _themeSettingsFile;


    public LocalThemeSettingsService(IFileService fileService)
    {
        _fileService = fileService;

        _applicationDataFolder = Path.Combine(_localApplicationData, DefaultApplicationDataFolder);
        _themeSettingsFile = ThemeSettingsFile;
    }

    public LocalThemeSettingsOptions? LoadThemeSettings()
    {
        try
        {
            return _fileService.Read<LocalThemeSettingsOptions>(_applicationDataFolder, _themeSettingsFile);
        }
        catch
        {
            return new LocalThemeSettingsOptions
            {
                AppBackgroundRequestedTheme = "Default",
                CustomThemes = DefaultThemes
            };
        }
    }

    public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings)
    {
        _fileService.Save(_applicationDataFolder, _themeSettingsFile, themeSettings);
    }

    public List<ThemeClass> GetDefaultThemes()
    {
        return DefaultThemes;
    }
}