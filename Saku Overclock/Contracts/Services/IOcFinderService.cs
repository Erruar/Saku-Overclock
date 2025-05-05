using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saku_Overclock.Contracts.Services;
public interface IOcFinderService
{
    void GeneratePremadeProfiles();
    string GetMinPreset();
    string GetEcoPreset();
    string GetBalPreset();
    string GetPerfPreset();
    string GetMaxPreset();
    (int[], int[], int[], int[], int[], int[], int[]) GetPerformanceRecommendationData();
}
