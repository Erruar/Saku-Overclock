using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IDataProvider
{
    /// <summary>
    ///     Получает и обновляет данные сенсоров для мониторинга.
    /// </summary>
    void GetData(ref SensorsInformation sensorsInformation);

    /// <summary>
    ///     Получить таблицу сенсоров устройства
    /// </summary>
    /// <returns>Таблица сенсоров устройства</returns>
    float[]? GetPowerTable();
}