using Saku_Overclock.Services;

namespace Saku_Overclock.Contracts.Services;

public interface IOcFinderService
{
    /// <summary>
    ///     Основная инициализация OcFinder, получает значение мощности процессора и строит по нему готовые пресеты и подсказки
    /// </summary>
    void LazyInitTdp();

    /// <summary>
    ///     Создаёт готовый пресет
    /// </summary>
    /// <param name="type">Тип требуемого пресета</param>
    /// <param name="level">Уровень оптимизации пресета</param>
    /// <returns>Конфигурация пресета: RyzenAdjLine, PresetMetrics, PresetOptions, доступность андервольтинга</returns>
    PresetConfiguration CreatePreset(PresetType type, OptimizationLevel level);

    /// <summary>
    ///     Получить рекомендации настроек пресета
    /// </summary>
    /// <returns>Рекомендации настроек</returns>
    PresetRecommendations GetPerformanceRecommendationData();

    /// <summary>
    ///     Узнать доступность андервольтинга
    /// </summary>
    /// <returns>Доступность андервольтинга</returns>
    bool IsUndervoltingAvailable();
}