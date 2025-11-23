using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Contracts.Services;

public interface IDataProvider
{
    /// <summary>
    ///  Получает и обновляет данные сенсоров для мониторинга.
    /// </summary>
    void GetData(ref SensorsInformation sensorsInformation);

    float[]? GetPowerTable();
}
