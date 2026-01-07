using Saku_Overclock.Services;

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
}