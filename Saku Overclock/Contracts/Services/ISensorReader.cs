namespace Saku_Overclock.Contracts.Services;

public interface ISensorReader
{
    /// <summary>
    /// Обновляет таблицу сенсоров из SMU
    /// </summary>
    bool RefreshTable();

    /// <summary>
    /// Читает значение сенсора по индексу в таблице
    /// </summary>
    (bool success, double value) ReadSensorByIndex(int index);

    /// <summary>
    /// Текущая версия таблицы PM
    /// </summary>
    int CurrentTableVersion
    {
        get;
    }

    /// <summary>
    /// Читает специальные значения, которые не находятся в основной таблице
    /// (например, MCLK, FCLK, VDDCR_SOC)
    /// </summary>
    (bool success, double value) ReadSpecialValue(string fieldName);

    /// <summary>
    /// Получает температуру процессора напрямую
    /// </summary>
    (bool success, double value) GetCpuTemperature();

    /// <summary>
    /// Получает множитель ядра для fallback вычислений
    /// </summary>
    (bool success, double value) GetCoreMulti(int coreIndex);

    /// <summary>
    /// Получает информацию о топологии процессора
    /// </summary>
    int GetTotalCoresTopology();

    /// <summary>
    /// Получает кодовое имя процессора
    /// </summary>
    string GetCodeName();

    /// <summary>
    /// Возвращает полную таблицу (для специальных случаев)
    /// </summary>
    float[]? GetFullTable();
}