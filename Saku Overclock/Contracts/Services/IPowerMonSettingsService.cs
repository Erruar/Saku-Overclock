namespace Saku_Overclock.Contracts.Services;

public interface IPowerMonSettingsService
{
    /// <summary>
    ///     Сохранение настроек
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Загрузка настроек
    /// </summary>
    void LoadSettings();
    
    /// <summary>
    ///     Настройки пользователя
    /// </summary>
    List<string> Notelist { get; set; }
}