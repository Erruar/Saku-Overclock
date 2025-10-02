using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Models;
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

    public LocalThemeSettingsOptions LoadThemeSettings()
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

    public void SaveThemeSettings(LocalThemeSettingsOptions themeSettings) =>
        _fileService.Save(_applicationDataFolder, _themeSettingsFile, themeSettings);

    public List<ThemeClass> GetDefaultThemes() => DefaultThemes;

    private static List<ThemeClass> DefaultThemes =>
    [
        new()
        {
            ThemeName = "Theme_Default",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new()
        {
            ThemeName = "Theme_Light",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new()
        {
            ThemeName = "Theme_Dark",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new()
        {
            ThemeName = "Theme_Clouds",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.5,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new()
        {
            ThemeName = "Theme_Neon",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.3,
            ThemeMaskOpacity = 0.3,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new()
        {
            ThemeName = "Theme_Raspberry",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 1.0,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/fw41KXN.png"
        },
        new()
        {
            ThemeName = "Theme_Sand",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.7,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/ZqjqlOs.png"
        },
        new()
        {
            ThemeName = "Theme_Coffee",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.3,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/ZqjqlOs.png"
        }
    ];
}