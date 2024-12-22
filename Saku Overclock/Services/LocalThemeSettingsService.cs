using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Core.Contracts.Services;
using Saku_Overclock.Core.Helpers;
using Saku_Overclock.Helpers; 
using Windows.Storage;

namespace Saku_Overclock.Services;

public class LocalThemeSettingsService : ILocalSettingsService
{
    private const string DefaultApplicationDataFolder = "Saku Overclock/Settings/Themes";
    private const string DefaultLocalSettingsFile = "ThemeSettings.json";

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
            _settings = await Task.Run(() => _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile));

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
}
