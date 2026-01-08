using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;
public interface ICustomSmuSettingsService
{
    /// <summary>
    ///     Сохранить настройки кастомных команд Smu
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Загрузить настройки кастомных команд Smu
    /// </summary>
    void LoadSettings();


    /// <summary>
    ///    Заметки пользователя
    /// </summary>
    string Note
    {
        get; 
        set;
    }


    /// <summary>
    ///     Коллекция кастомных наборов адресов Smu
    /// </summary>
    List<CustomMailBoxes>? MailBoxes
    {
        get;
        set;
    }

    /// <summary>
    ///     Коллекция кастомных команд Smu
    /// </summary>
    List<QuickSmuCommands>? QuickSmuCommands
    {
        get;
        set;
    }
}