using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IBackgroundDataUpdater
{
    /// <summary>
    ///     Запускает сервис обновления данных с сенсоров устройства
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    void StartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Остановить обновление данных
    /// </summary>
    void Stop();

    /// <summary>
    ///     Обновить состояние TrayMon иконок
    /// </summary>
    void UpdateTrayMonIcons();

    /// <summary>
    ///     Событие, возвращающее полученные данные с сенсоров устройства
    /// </summary>
    event EventHandler<SensorsInformation> DataUpdated;

    /// <summary>
    ///     Возвращает доступность сенсоров батареи
    /// </summary>
    /// <returns>Доступность сенсоров батареи</returns>
    bool IsBatteryUnavailable();
}