using Saku_Overclock.Styles;

namespace Saku_Overclock.Contracts.Services;

public interface ILocalSettingsService
{
    Task<T?> ReadSettingAsync<T>(string key);

    Task SaveSettingAsync<T>(string key, T value);

    public List<ThemeClass> LoadCustomThemes();

    public void SaveCustomThemes(List<ThemeClass> themes);
}
