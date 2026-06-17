using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface INotifyIconsService
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
    public List<NiIconsElements> Elements
    {
        get;
        set;
    }

    /// <summary>
    ///     Создаёт трей иконки
    /// </summary>
    public void CreateNotifyIcons();
    
    /// <summary>
    ///     Обновить состояние иконок со стороны страницы
    /// </summary>
    void UpdateTrayMonIcons();

    /// <summary>
    ///     Обновить данные в иконках
    /// </summary>
    /// <param name="sensorsInformation">Данные сенсоров</param>
    public void UpdateNotifyIcons(SensorsInformation sensorsInformation);

    /// <summary>
    ///     Уничтожит все активные иконки
    /// </summary>
    public void DisposeAllNotifyIcons();

    /// <summary>
    ///     Были ли созданы иконки
    /// </summary>
    public bool IsIconsCreated
    {
        get; 
        set;
    }
    
    
    /// <summary>
    ///     Были ли обновлены иконки
    /// </summary>
    public bool IsIconsUpdated
    {
        get;
        set;
    }
}