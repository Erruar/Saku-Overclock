using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Saku_Overclock.Services;

namespace Saku_Overclock.Contracts.Services;
public interface IOcFinderService
{
    void LazyInitTdp(); // Основной инит платформы
    PresetConfiguration CreatePreset(PresetType type, OptimizationLevel level);
    PresetMetrics GetPresetMetrics(PresetType type, OptimizationLevel level);
    PresetOptions GetPresetOptions(string preset);

    void GeneratePremadeProfiles();
    bool IsUndervoltingAvailable();
    void ClearMetricsCache();
    string CurveOptimizerGenerateStringHelper(int value);

    (int[], int[], int[], int[], int[], int[], int[]) GetPerformanceRecommendationData();
}
