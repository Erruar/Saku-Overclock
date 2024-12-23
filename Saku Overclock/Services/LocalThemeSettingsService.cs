using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Core.Helpers;
using Saku_Overclock.Helpers; 
using Windows.Storage;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Services;

public class LocalThemeSettingsService : ILocalSettingsService
{
    private const string DefaultApplicationDataFolder = "Saku Overclock/Settings/Themes";
    private const string DefaultLocalSettingsFile = "ThemeSettings.json";
    private const string CustomLocalSettingsFile = "CustomThemeSettings.json";

    private readonly IFileService _fileService;

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public LocalThemeSettingsService(IFileService fileService)
    {
        _fileService = fileService;

        _applicationDataFolder = Path.Combine(_localApplicationData, DefaultApplicationDataFolder);
        _localsettingsFile =  DefaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = await Task.Run(() => _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile)) ?? new Dictionary<string, object>();

            _isInitialized = true;
        }
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings.Any() && _settings.TryGetValue(key, out var obj))
            {
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value!);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = await Json.StringifyAsync(value!);
            try
            {
                await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
            }
            catch
            {
                //Can't change theme!
            }
        }
    }
    
    public List<ThemeClass> LoadCustomThemes()
    {
        try
        {
            var themes = _fileService.Read<List<ThemeClass>>(_applicationDataFolder, CustomLocalSettingsFile);
            if (themes != null)
            {
                return themes;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e); 
        }
        return [
        new ()
        {
            ThemeName = "Theme_Default",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Light",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Dark",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.0,
            ThemeMaskOpacity = 0.0,
            ThemeCustomBg = false,
            ThemeBackground = ""
        },
        new ()
        {
            ThemeName = "Theme_Clouds",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.5,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new ()
        {
            ThemeName = "Theme_Neon",
            ThemeLight = false,
            ThemeCustom = false,
            ThemeOpacity = 0.3,
            ThemeMaskOpacity = 0.3,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/DuwlKmK.png"
        },
        new ()
        {
            ThemeName = "Theme_Raspberry",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 1.0, 
            ThemeMaskOpacity = 1.0, 
            ThemeCustomBg = false, 
            ThemeBackground = "ms-appx:///Assets/Themes/fw41KXN.png"
        },
        new ()
        {
            ThemeName = "Theme_Sand",
            ThemeLight = true,
            ThemeCustom = false,
            ThemeOpacity = 0.7,
            ThemeMaskOpacity = 1.0,
            ThemeCustomBg = false,
            ThemeBackground = "ms-appx:///Assets/Themes/ZqjqlOs.png"
        },
        new ()
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

    public void SaveCustomThemes(List<ThemeClass> themes)
    {
        _fileService.Save(_applicationDataFolder, CustomLocalSettingsFile, themes);
    }
}
