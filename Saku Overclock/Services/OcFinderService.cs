using System.Text;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;
using ZenStates.Core;
using static ZenStates.Core.Cpu;

namespace Saku_Overclock.Services;

public enum OptimizationLevel
{
    Basic,      // Только безопасные настройки
    Standard,   // Стандартные настройки
    Deep        // Стандартные + Андервольтинг + Агрессивные настройки
}

public enum PresetType
{
    Min,
    Eco,
    Balance,
    Performance,
    Max
}

public class PresetMetrics
{
    public int PerformanceScore
    {
        get; set;
    }     // -50 - +50
    public int EfficiencyScore
    {
        get; set;
    }     // -50 - +50
    public int ThermalScore
    {
        get; set;
    }        // -50 - +50
}
public class PresetOptions
{
    public string ThermalOptions
    {
        get; set;
    } = string.Empty;
    public string PowerOptions
    {
        get; set;
    } = string.Empty;
    public string CurrentOptions
    {
        get; set;
    } = string.Empty;
}

public class PresetConfiguration
{
    public string CommandString { get; set; } = "";
    public PresetMetrics Metrics { get; set; } = new();
    public PresetOptions Options { get; set; } = new();
    public PresetType Type
    {
        get; set;
    }
    public OptimizationLevel Level
    {
        get; set;
    }
    public bool IsUndervoltingEnabled
    {
        get; set;
    }
}

public class PresetCurves
{
    public double[] MinCurve { get; set; } = new double[4];
    public double[] EcoCurve { get; set; } = new double[4];
    public double[] BalanceCurve { get; set; } = new double[4];
    public double[] PerformanceCurve { get; set; } = new double[4];
    public double[] MaxCurve { get; set; } = new double[4];
    public double[] FastMultipliers { get; set; } = new double[2]; // Только коэффициенты для fast = a * stapm + b
}

public class ArchitectureProfile
{
    public string Name { get; set; } = "";
    public PresetCurves LaptopCurves { get; set; } = new();
    public PresetCurves DesktopCurves { get; set; } = new();
    public double EfficiencyMultiplier { get; set; } = 1.0;
    public double ThermalMultiplier { get; set; } = 1.0;
    public int MaxSafeTempBasic { get; set; } = 75;
    public int MaxSafeTempStandard { get; set; } = 90;
    public int MaxSafeTempDeep { get; set; } = 95;

    // Дополнительные множители для тонкой настройки архитектуры
    public double StapmBonus { get; set; } = 0.0; // Добавляется к результату кривой
    public double FastBonus { get; set; } = 0.0;  // Добавляется к результату fast расчета
}

public class SafetyLimits
{
    public int MaxTempBasic { get; set; } = 75;
    public int MaxTempStandard { get; set; } = 90;
    public int MaxTempDeep { get; set; } = 95;

    public double MaxStapmMultiplierBasic { get; set; } = 1.1;
    public double MaxStapmMultiplierStandard { get; set; } = 1.3;
    public double MaxStapmMultiplierDeep { get; set; } = 1.5;

    public int MinStapmTime { get; set; } = 200;
    public int MaxStapmTime { get; set; } = 900;
}

public class OcFinderService : IOcFinderService
{
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private readonly IDataProvider? _dataProvider = App.GetService<IDataProvider>();
    private Cpu _cpu;

    private const bool _forceTraining = false;
    private bool _isInitialized = false;
    private bool _isTdpInitialized = false;
    private bool _isUndervoltingAvailable = false;
    private bool _isUndervoltingChecked = false;

    // Кэшированные значения TDP
    private double _validatedCpuPower = 35.0;
    private bool _isPlatformPC = false;

    private readonly SafetyLimits _safetyLimits = new();
    private readonly Dictionary<string, ArchitectureProfile> _architectureProfiles = [];

    // Кэш для метрик
    private readonly Dictionary<string, PresetMetrics> _metricsCache = [];
    private readonly Dictionary<string, PresetOptions> _optionsCache = [];

    public OcFinderService()
    {
        _cpu = CpuSingleton.GetInstance();
        InitializeArchitectureProfiles();
    }

    /// <summary>
    /// Ленивая инициализация TDP - вызывается только при первом обращении
    /// </summary>
    public void LazyInitTdp()
    {
        if (_isTdpInitialized)
        {
            return;
        }

        _cpu ??= CpuSingleton.GetInstance();
        var cpuPower = SendSmuCommand.ReturnCpuPowerLimit(_cpu);
        CheckUndervoltingFeature();
        var powerTable = _dataProvider?.GetPowerTable();
        var powerTableCheckError = false;
        var checkupCpuPower = 35d;

        if (powerTable != null && powerTable.Length > 3)
        {
            var powerLimit = powerTable[0];
            var realPower = powerTable[2];
            var avgPower = powerTable[4];

            // Трёхкратная проверка для точного определения текущего лимита мощности
            if (powerLimit != 0)
            {
                checkupCpuPower = powerLimit;
            }
            else if (realPower != 0)
            {
                checkupCpuPower = realPower;
            }
            else if (avgPower != 0)
            {
                checkupCpuPower = avgPower;
            }
            else
            {
                powerTableCheckError = true;
            }
        }
        else
        {
            powerTableCheckError = true;
        }

        if (!powerTableCheckError && cpuPower > checkupCpuPower)
        {
            cpuPower = checkupCpuPower;
        }

        _isPlatformPC = SendSmuCommand.IsPlatformPC(_cpu) == true;
        _validatedCpuPower = cpuPower;

        // Ограничение для мобильных платформ
        if (_validatedCpuPower > 45 && !_isPlatformPC)
        {
            _validatedCpuPower = 45d;
        }

        if (_cpu.info.codeName == Cpu.CodeName.BristolRidge) 
        {
            _validatedCpuPower = 35d;
        }

        _isTdpInitialized = true;
    }

    /// <summary>
    /// Проверка доступности андервольтинга
    /// </summary>
    private bool CheckUndervoltingFeature()
    {
        if (_isUndervoltingChecked)
        {
            return _isUndervoltingAvailable;
        }

        _isUndervoltingAvailable = SendSmuCommand.ReturnUndervoltingAvailability(_cpu);
        _isUndervoltingChecked = true;
        return _isUndervoltingAvailable;
    }

    /// <summary>
    /// Инициализация профилей архитектур с кривыми для каждого типа пресета
    /// </summary>
    private void InitializeArchitectureProfiles()
    {
        // Базовый профиль (используется для всех архитектур как основа)
        var baseProfile = new ArchitectureProfile
        {
            Name = "Base",
            LaptopCurves = new PresetCurves
            {
                MinCurve = [0.00130000, -0.1228000, 3.5876, -24.5430],
                EcoCurve = [0.00060000, -0.0630000, 2.3241, -10.8710],
                BalanceCurve = [0.00002262, -0.0004827, 0.9628, 2.92400],
                PerformanceCurve = [-0.0003000, 0.0292000, 0.2543, 13.3532],
                MaxCurve = [-0.0009000, 0.0776000, -0.8828, 28.8492],
                FastMultipliers = [1.17335141, 0.21631949]
            },
            DesktopCurves = new PresetCurves
            {
                MinCurve = [0.000034510, -0.011510, 1.52400, -25.52000],
                EcoCurve = [0.000017110, -0.006530, 1.16600, -8.417000],
                BalanceCurve = [-0.00002563, 0.005565, 0.63570, 7.069000],
                PerformanceCurve = [-0.00006658, 0.021440, -1.14200, 81.64000],
                MaxCurve = [-0.00005653, 0.021680, -1.21900, 98.47000],
                FastMultipliers = [1.0, 0.0] // Для десктопа fast = stapm
            },
            EfficiencyMultiplier = 1.0,
            ThermalMultiplier = 1.0
        };

        // PreZen профиль
        _architectureProfiles["PreZen"] = new ArchitectureProfile
        {
            Name = "PreZen",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 0.9,
            ThermalMultiplier = 0.95,
            StapmBonus = 2.0, // Завышаеи мощность для старой архитектуры
            FastBonus = 1.0
        };

        // Zen профиль
        _architectureProfiles["Zen"] = new ArchitectureProfile
        {
            Name = "Zen",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 1.0,
            ThermalMultiplier = 1.0
        };

        // Zen2 профиль
        _architectureProfiles["Zen2"] = new ArchitectureProfile
        {
            Name = "Zen2",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 1.05,
            ThermalMultiplier = 1.0,
            StapmBonus = 1.0
        };

        // Zen3 профиль (базовый)
        _architectureProfiles["Zen3"] = new ArchitectureProfile
        {
            Name = "Zen3",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 1.05,
            ThermalMultiplier = 1.0,
            StapmBonus = 1.5
        };

        // Zen4 профиль (более эффективный)
        _architectureProfiles["Zen4"] = new ArchitectureProfile
        {
            Name = "Zen4",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 1.15,
            ThermalMultiplier = 1.10,
            StapmBonus = 2.5,
            FastBonus = 1.0
        };

        // Zen5 профиль (самый эффективный)
        _architectureProfiles["Zen5"] = new ArchitectureProfile
        {
            Name = "Zen5",
            LaptopCurves = baseProfile.LaptopCurves,
            DesktopCurves = baseProfile.DesktopCurves,
            EfficiencyMultiplier = 1.25,
            ThermalMultiplier = 1.20,
            StapmBonus = 3.5,
            FastBonus = 2.0
        };
    }

    /// <summary>
    /// Получение профиля архитектуры на основе CPU
    /// </summary>
    private ArchitectureProfile GetArchitectureProfile()
    {
        if (_cpu?.info.codeName != null)
        {
            var codenameGeneration = SendSmuCommand.GetCodeNameGeneration(_cpu);
            return codenameGeneration switch
            {
                "FP4" => _architectureProfiles["PreZen"],
                "FP5" => _architectureProfiles["Zen"],
                "FF3" => _architectureProfiles["Zen2"],
                "FP6" => _architectureProfiles["Zen3"],
                "FP7" => _architectureProfiles["Zen4"],
                "FP8" => _architectureProfiles["Zen5"],
                "AM4_V1" => _architectureProfiles["Zen2"],
                "AM4_V2" => _architectureProfiles["Zen3"],
                "AM5" => _architectureProfiles["Zen"],
                _ => _architectureProfiles["Zen3"]
            };
        }

        return _architectureProfiles["Zen3"]; // По умолчанию
    }

    /// <summary>
    /// Расчет полиномиальной аппроксимации
    /// </summary>
    private static double CalculatePolynomial(double[] coefficients, double x)
    {
        double result = 0;
        for (var i = 0; i < coefficients.Length; i++)
        {
            result += coefficients[i] * Math.Pow(x, coefficients.Length - 1 - i);
        }
        return result;
    }

    /// <summary>
    /// Получение кривой для конкретного типа пресета
    /// </summary>
    private static double[] GetCurveForPresetType(PresetType type, PresetCurves curves)
    {
        return type switch
        {
            PresetType.Min => curves.MinCurve,
            PresetType.Eco => curves.EcoCurve,
            PresetType.Balance => curves.BalanceCurve,
            PresetType.Performance => curves.PerformanceCurve,
            PresetType.Max => curves.MaxCurve,
            _ => curves.BalanceCurve
        };
    }

    /// <summary>
    /// Создание пресета с использованием специфичных кривых для каждого типа
    /// </summary>
    public PresetConfiguration CreatePreset(PresetType type, OptimizationLevel level)
    {
        LazyInitTdp();

        var profile = GetArchitectureProfile();
        var curves = _isPlatformPC ? profile.DesktopCurves : profile.LaptopCurves;

        // Получаем кривую для конкретного типа пресета
        var presetCurve = GetCurveForPresetType(type, curves);

        // Рассчитываем STAPM напрямую из кривой пресета
        var stapmValue = CalculatePolynomial(presetCurve, _validatedCpuPower);

        // Добавляем архитектурный бонус
        stapmValue += profile.StapmBonus;

        // Модификация под уровень оптимизации (небольшая корректировка)
        var levelBonus = level switch
        {
            OptimizationLevel.Basic => type == PresetType.Min ? 0 : -1.0,
            OptimizationLevel.Standard => 0,
            OptimizationLevel.Deep => type == PresetType.Max ? 0 : 1.5,
            _ => 0
        };

        stapmValue += levelBonus;

        // Минимальный лимит
        if (stapmValue < 6)
        {
            stapmValue = 6;
        }

        // Расчет Fast limit
        var fastValue = _isPlatformPC ?
            stapmValue + profile.FastBonus :
            FromValueToUpper(curves.FastMultipliers[0] * stapmValue + curves.FastMultipliers[1] + profile.FastBonus, 3);

        // Температурные лимиты
        var tempLimit = level switch
        {
            OptimizationLevel.Basic => Math.Min(profile.MaxSafeTempBasic, GetBaseTempForPreset(type)),
            OptimizationLevel.Standard => Math.Min(profile.MaxSafeTempStandard, GetBaseTempForPreset(type)),
            OptimizationLevel.Deep => Math.Min(profile.MaxSafeTempDeep, GetBaseTempForPreset(type)),
            _ => GetBaseTempForPreset(type)
        };

        // Временные параметры
        var (stapmTime, slowTime, prochotRamp) = GetTimingParameters(type, level);

        var commandString = BuildCommandString(stapmValue, fastValue, tempLimit, stapmTime, slowTime, prochotRamp, level);

        var preset = new PresetConfiguration
        {
            Type = type,
            Level = level,
            IsUndervoltingEnabled = level == OptimizationLevel.Deep && _isUndervoltingAvailable,
            CommandString = commandString,
            Metrics = CalculateMetrics(type, level, stapmValue, tempLimit, profile),
            Options = GetPresetOptions(commandString)
        };

        return preset;
    }

    private static int GetBaseTempForPreset(PresetType type) => type switch
    {
        PresetType.Min => 60,
        PresetType.Eco => 70,
        PresetType.Balance => 90,
        PresetType.Performance => 90,
        PresetType.Max => 100,
        _ => 80
    };

    private (int stapmTime, int slowTime, int prochotRamp) GetTimingParameters(PresetType type, OptimizationLevel level)
    {
        var baseTiming = type switch
        {
            PresetType.Min => (900, 900, 2),
            PresetType.Eco => (500, 500, 2),
            PresetType.Balance => (300, 5, 20),
            PresetType.Performance => (200, 3, 200),
            PresetType.Max => (100, 2, 100),
            _ => (300, 5, 20)
        };

        // Для глубокой оптимизации без андервольтинга - более агрессивные тайминги
        if (level == OptimizationLevel.Deep && !_isUndervoltingAvailable)
        {
            baseTiming.Item1 = Math.Max(baseTiming.Item1 - 100, _safetyLimits.MinStapmTime);
            baseTiming.Item2 = Math.Max(baseTiming.Item2 - 1, 1);
        }

        return baseTiming;
    }

    private string BuildCommandString(double stapm, double fast, int tempLimit, int stapmTime, int slowTime, int prochotRamp, OptimizationLevel level)
    {
        var sb = new StringBuilder();

        sb.Append($"--fast-limit={(int)(fast * 1000)} ");

        if (_cpu.info.codeName != CodeName.BristolRidge)
        {
            sb.Append($"--tctl-temp={tempLimit} ");

            // DragonRange is laptop CPU but with Desktop silicon and has Stapm limit
            var codenameGen = SendSmuCommand.GetCodeNameGeneration(_cpu);
            if (codenameGen == "AM5" && _cpu.info.codeName == CodeName.DragonRange || codenameGen != "AM5")
            {
                sb.Append($"--stapm-limit={(int)(stapm * 1000)} ");
            }
            sb.Append($"--slow-limit={(int)(stapm * 1000)} ");
            sb.Append($"--stapm-time={stapmTime} ");
            sb.Append($"--slow-time={slowTime} ");
            sb.Append($"--vrm-current=120000 ");
            sb.Append($"--vrmmax-current=140000 ");
            sb.Append($"--vrmsoc-current=100000 ");
            sb.Append($"--vrmsocmax-current=110000 ");
        }
        else
        {
            var tempLimitBr = tempLimit > 85 ? 84000 : tempLimit * 1000;
            sb.Append($"--tctl-temp={tempLimitBr} ");
            sb.Append($"--stapm-limit={(int)(stapm * 1000)},2,{stapmTime * 1000} ");
            sb.Append($"--max-performance=0 "); // Включит Max Performance режим
            sb.Append($"--disable-feature=10 "); // Выключит Pkg-Pwr лимит

            if (level == OptimizationLevel.Deep) 
            {
                sb.Append($"--disable-feature=8 "); // Выключит TDC лимит
            }
        }

        sb.Append($"--prochot-deassertion-ramp={prochotRamp} ");

        // Для глубокой оптимизации добавляем андервольтинг если доступен
        if (level == OptimizationLevel.Deep && _isUndervoltingAvailable)
        {
            sb.Append(CurveOptimizerGenerateStringHelper(-10)); // Базовый андервольтинг
        }

        return sb.ToString().Trim();
    }

    public string CurveOptimizerGenerateStringHelper(int value) => (value >= 0) ?
        $" --set-coall={value} " : $" --set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * value))} ";

    private PresetMetrics CalculateMetrics(PresetType type, OptimizationLevel level, double stapm, int tempLimit, ArchitectureProfile profile)
    {
        var cacheKey = $"{type}_{level}_{stapm:F1}_{tempLimit}";
        if (_metricsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Константы для настройки
        const double BASE_TEMP = 85.0;  // Базовая температура для расчетов
        const double PERFORMANCE_CURVE_POWER = 0.45; // Степень для кривой производительности (меньше 0.5 = более пологая кривая)
        const double EFFICIENCY_SWEET_SPOT = 0.8; // Точка максимальной энергоэффективности (75% от базовой мощности)

        // Нормализованная мощность (0 = минимум, 1 = база, >1 = буст)
        var powerRatio = stapm / _validatedCpuPower;

        // === ПРОИЗВОДИТЕЛЬНОСТЬ ===
        // Используем корневую зависимость с убывающей отдачей
        // При powerRatio = 1 (100% мощности) -> performanceBase = 0
        // При powerRatio = 2 (200% мощности) -> performanceBase ≈ 32 (вместо 50 при линейной)
        double performanceBase;
        if (powerRatio >= 1.0)
        {
            // Буст режим: убывающая отдача от повышения мощности
            var boost = powerRatio - 1.0;
            performanceBase = Math.Pow(boost, PERFORMANCE_CURVE_POWER) * 50;
        }
        else
        {
            // Эко режим: более резкое падение производительности
            var reduction = 1.0 - powerRatio;
            performanceBase = -Math.Pow(reduction, PERFORMANCE_CURVE_POWER * 0.8) * 50;
        }

        // === ТЕМПЕРАТУРЫ ===
        // Чем ниже лимит температуры, тем лучше для системы
        var tempRatio = tempLimit / BASE_TEMP;
        double thermalScore;

        if (tempRatio <= 0.85) // Агрессивное охлаждение (≤72°C)
        {
            thermalScore = (tempRatio - 0.85) * 200; // Более холодная работа
        }
        else if (tempRatio <= 1.0) // Нормальное охлаждение (72-85°C)
        {
            thermalScore = (tempRatio - 1.0) * 100 - 15; // Слегка больше
        }
        else // Горячий режим (>85°C)
        {
            thermalScore = (tempRatio - 1.0) * 200; // Температура сильно больше 
        }

        // Применяем термальный множитель профиля
        thermalScore *= profile.ThermalMultiplier;

        // === ЭНЕРГОЭФФЕКТИВНОСТЬ ===
        // Максимальная эффективность в "сладкой точке" около 75% мощности
        double efficiencyBase;

        // Расстояние от оптимальной точки
        var distanceFromSweet = Math.Abs(powerRatio - EFFICIENCY_SWEET_SPOT);

        // Базовая эффективность - колоколообразная кривая с центром в EFFICIENCY_SWEET_SPOT
        efficiencyBase = 50 * Math.Exp(-Math.Pow(distanceFromSweet * 2, 2));

        // Корректировка на основе EfficiencyMultiplier
        // Высокий multiplier (1.25) = процессор эффективен на низких мощностях
        // Низкий multiplier (0.8) = процессор менее эффективен, но лучше масштабируется
        if (profile.EfficiencyMultiplier > 1.0)
        {
            // Эффективный процессор: лучше работает на низких мощностях
            if (powerRatio < 1.0)
            {
                efficiencyBase *= profile.EfficiencyMultiplier;
            }
            else
            {
                // Меньше выигрыша от повышения мощности
                efficiencyBase *= (2.0 - profile.EfficiencyMultiplier);
            }
        }
        else
        {
            // Менее эффективный процессор: лучше масштабируется с мощностью
            if (powerRatio > 1.0)
            {
                // Больше выигрыша от повышения мощности
                efficiencyBase *= (1.0 + (1.0 - profile.EfficiencyMultiplier));
            }
            else
            {
                efficiencyBase *= profile.EfficiencyMultiplier;
            }
        }

        // Учитываем температуру в эффективности
        efficiencyBase -= Math.Max(0, -thermalScore) * 0.3; // Штраф за высокие температуры

        // === БОНУСЫ ЗА УРОВЕНЬ ОПТИМИЗАЦИИ ===
        var isEcoPreset = type == PresetType.Eco || type == PresetType.Min;

        var performanceLevelBonus = 0;
        var efficiencyLevelBonus = 0;
        var thermalLevelBonus = 0;

        switch (level)
        {
            case OptimizationLevel.Basic:
                if (isEcoPreset)
                {
                    efficiencyLevelBonus = 5;
                    thermalLevelBonus = 3;
                    performanceLevelBonus = -2;
                }
                else
                {
                    performanceLevelBonus = 3;
                    efficiencyLevelBonus = -2;
                }
                break;

            case OptimizationLevel.Standard:
                // Сбалансированный подход
                performanceLevelBonus = 0;
                efficiencyLevelBonus = 2;
                thermalLevelBonus = 0;
                break;

            case OptimizationLevel.Deep:
                if (isEcoPreset)
                {
                    efficiencyLevelBonus = -3; // Глубокая оптимизация может снизить эффективность
                    thermalLevelBonus = -2;
                    performanceLevelBonus = 5; // Но повысить производительность
                }
                else
                {
                    performanceLevelBonus = 5;
                    efficiencyLevelBonus = 3;
                    thermalLevelBonus = -3; // Может повысить температуры
                }
                break;
        }

        // === ФИНАЛЬНЫЕ ЗНАЧЕНИЯ ===
        var metrics = new PresetMetrics
        {
            PerformanceScore = Math.Clamp((int)Math.Round(performanceBase) + performanceLevelBonus, -50, 50),
            EfficiencyScore = Math.Clamp((int)Math.Round(efficiencyBase) + efficiencyLevelBonus, -50, 50),
            ThermalScore = Math.Clamp((int)Math.Round(thermalScore) + thermalLevelBonus, -50, 50)
        };

        _metricsCache[cacheKey] = metrics;
        return metrics;
    } 

    // Публичные методы для теста андервольтинга
    public bool IsUndervoltingAvailable()
    {
        return CheckUndervoltingFeature();
    }

    public PresetMetrics GetPresetMetrics(PresetType type, OptimizationLevel level)
    {
        var preset = CreatePreset(type, level);
        return preset.Metrics;
    }
    public PresetOptions GetPresetOptions(string preset)
    {
        var cacheKey = $"{preset}";
        if (_optionsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }


        var parts = preset.Split(' ');
        var values = new Dictionary<string, int>();

        foreach (var part in parts)
        {
            if (part.Contains('='))
            {
                var keyValue = part.Split('=');
                if (keyValue.Length >= 2 && int.TryParse(keyValue[1], out var value))
                {
                    values[keyValue[0]] = value;
                }
            }
        }

        var options = new PresetOptions()
        {
            ThermalOptions = values.GetValueOrDefault("--tctl-temp", 80).ToString() + "C",
            PowerOptions = (values.GetValueOrDefault("--stapm-limit", 35000) / 1000).ToString() + "W, "
            + (values.GetValueOrDefault("--fast-limit", 35000) / 1000).ToString() + "W, "
            + (values.GetValueOrDefault("--slow-limit", 35000) / 1000).ToString() + "W",
            CurrentOptions = (values.GetValueOrDefault("--vrmmax-current", 110000) / 1000).ToString() + "A, " + (values.GetValueOrDefault("--vrm-current", 90000) / 1000).ToString() + "A, " + (values.GetValueOrDefault("--vrmsocmax-current", 50000) / 1000).ToString() + "A"
        };

        _optionsCache[cacheKey] = options;

        return options;
    }

    public void ClearMetricsCache()
    {
        _metricsCache.Clear();
    }

    private static int FromValueToUpper(double value, int upper) => (int)Math.Ceiling(value / upper) * upper;

    // Legacy методы для совместимости
    public void GeneratePremadeProfiles()
    {
        if (_isInitialized && !_forceTraining)
        {
            return;
        }

        LazyInitTdp();
        _isInitialized = true;
    }

    public (int[], int[], int[], int[], int[], int[], int[]) GetPerformanceRecommendationData()
    {
        if (!_isInitialized)
        {
            GeneratePremadeProfiles();
        }

        // Генерируем данные на основе Balance и Performance пресетов
        var balancePreset = CreatePreset(PresetType.Balance, OptimizationLevel.Standard);
        var performancePreset = CreatePreset(PresetType.Performance, OptimizationLevel.Standard);

        // Извлекаем значения из строк команд (упрощенно)
        var balanceValues = ParseCommandString(balancePreset.CommandString);
        var performanceValues = ParseCommandString(performancePreset.CommandString);

        return new(
            [balanceValues.TempLimit, performanceValues.TempLimit],
            [balanceValues.StapmLimit, performanceValues.StapmLimit],
            [balanceValues.FastLimit, performanceValues.FastLimit],
            [balanceValues.SlowLimit, performanceValues.SlowLimit],
            [balanceValues.SlowTime, performanceValues.SlowTime],
            [balanceValues.StapmTime, performanceValues.StapmTime],
            [balanceValues.ProchotRamp, performanceValues.ProchotRamp]
        );
    }

    private (int TempLimit, int StapmLimit, int FastLimit, int SlowLimit, int SlowTime, int StapmTime, int ProchotRamp) ParseCommandString(string commandString)
    {
        // Простой парсер для извлечения значений из строки команд
        var parts = commandString.Split(' ');
        var values = new Dictionary<string, int>();

        foreach (var part in parts)
        {
            if (part.Contains('='))
            {
                var keyValue = part.Split('=');
                if (keyValue.Length >= 2 && int.TryParse(keyValue[1], out var value))
                {
                    values[keyValue[0]] = value;
                }
            }
        }

        return (
            values.GetValueOrDefault("--tctl-temp", 80),
            values.GetValueOrDefault("--stapm-limit", 35000) / 1000,
            values.GetValueOrDefault("--fast-limit", 35000) / 1000,
            values.GetValueOrDefault("--slow-limit", 35000) / 1000,
            values.GetValueOrDefault("--slow-time", 5),
            values.GetValueOrDefault("--stapm-time", 300),
            values.GetValueOrDefault("--prochot-deassertion-ramp", 20)
        );
    }
}