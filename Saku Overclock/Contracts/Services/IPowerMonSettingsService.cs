namespace Saku_Overclock.Contracts.Services;

public interface IPowerMonSettingsService
{
    /// <summary>
    ///     Загрузка настроек
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     Настройки пользователя
    /// </summary>
    List<string> Notelist { get; }
}