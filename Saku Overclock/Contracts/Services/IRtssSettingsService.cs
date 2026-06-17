using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IRtssSettingsService
{
    /// <summary>
    ///     Сохранить настройки Rtss
    /// </summary>
    void SaveSettings();

    /// <summary>
    ///     Загрузить настройки Rtss
    /// </summary>
    void LoadSettings();

    /// <summary>
    ///     Коллекция элементов Rtss для отображения
    /// </summary>
    List<RtssElementsClass> RtssElements
    {
        get;
    }

    /// <summary>
    ///     Включен ли редактор кода оверлея Rtss
    /// </summary>
    bool IsAdvancedCodeEditorEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Строка кода оверлея Rtss (используется для отрисовки оверлея)
    /// </summary>
    string AdvancedCodeEditor
    {
        get;
        set;
    }
    
    /// <summary>
    ///     Был ли RTSS обновлён
    /// </summary>
    public bool IsRtssUpdated
    {
        get; 
        set;
    }

    /// <summary>
    ///     Обновление отображаемых параметров оверлея
    /// </summary>
    /// <param name="sensorsInformation">Данные сенсоров</param>
    /// <param name="appliedPreset">Выбранный пресет</param>
    /// <param name="coreCount">Количество ядер</param>
    public void UpdateRtssMetrics(SensorsInformation sensorsInformation, string? appliedPreset, int? coreCount);
}