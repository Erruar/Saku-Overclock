using System.Text;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Services;

public enum OptimizationLevel
{
    Basic, // Только безопасные настройки
    Standard, // Стандартные настройки
    Deep // Стандартные + Андервольтинг + Агрессивные настройки
}

public enum PresetType
{
    Min,
    Eco,
    Balance,
    Speed,
    Max
}

public class PresetMetrics
{
    public int PerformanceScore
    {
        get;
        init;
    } // -50 - +50

    public int EfficiencyScore
    {
        get;
        init;
    } // -50 - +50

    public int ThermalScore
    {
        get;
        init;
    } // -50 - +50
}

public class PresetOptions
{
    public string ThermalOptions
    {
        get;
        init;
    } = string.Empty;

    public string PowerOptions
    {
        get;
        init;
    } = string.Empty;

    public string CurrentOptions
    {
        get;
        init;
    } = string.Empty;
}

public class PresetConfiguration
{
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public string CommandString
    {
        get;
        set;
    } = "";

    public PresetMetrics Metrics
    {
        get;
        init;
    } = new();

    public PresetOptions Options
    {
        get;
        init;
    } = new();

    public PresetType Type
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
        set;
    }

    public OptimizationLevel Level
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
        set;
    }

    public bool IsUndervoltingEnabled
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        get;
        set;
    }
}

public class PresetCurves
{
    public double[] MinCurve
    {
        get;
        init;
    } = new double[4];

    public double[] EcoCurve
    {
        get;
        init;
    } = new double[4];

    public double[] BalanceCurve
    {
        get;
        init;
    } = new double[4];

    public double[] PerformanceCurve
    {
        get;
        init;
    } = new double[4];

    public double[] MaxCurve
    {
        get;
        init;
    } = new double[4];

    public double[] FastMultipliers
    {
        get;
        init;
    } = new double[2]; // Только коэффициенты для fast = a * stapm + b
}

public class ArchitecturePreset
{
    public PresetCurves LaptopCurves
    {
        get;
        init;
    } = new();

    public PresetCurves DesktopCurves
    {
        get;
        init;
    } = new();

    public double EfficiencyMultiplier
    {
        get;
        init;
    } = 1.0;

    public double ThermalMultiplier
    {
        get;
        init;
    } = 1.0;

    public static int MaxSafeTempBasic => 75;
    public static int MaxSafeTempStandard => 90;
    public static int MaxSafeTempDeep => 95;

    // Дополнительные множители для тонкой настройки архитектуры
    public double StapmBonus
    {
        get;
        init;
    } // Добавляется к результату кривой

    public double FastBonus
    {
        get;
        init;
    } // Добавляется к результату fast расчета
}

public class SafetyLimits
{
    public int MaxTempBasic
    {
        get;
        set;
    } = 75;

    public int MaxTempStandard
    {
        get;
        set;
    } = 90;

    public int MaxTempDeep
    {
        get;
        set;
    } = 95;

    public double MaxStapmMultiplierBasic
    {
        get;
        set;
    } = 1.1;

    public double MaxStapmMultiplierStandard
    {
        get;
        set;
    } = 1.3;

    public double MaxStapmMultiplierDeep
    {
        get;
        set;
    } = 1.5;

    public int MinStapmTime
    {
        get;
        set;
    } = 200;

    public int MaxStapmTime
    {
        get;
        set;
    } = 900;
}

public class OcFinderService : IOcFinderService
{
    private static readonly ISendSmuCommandService SendSmuCommand = App.GetService<ISendSmuCommandService>();
    private readonly IDataProvider? _dataProvider = App.GetService<IDataProvider>();
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly ICpuService Cpu = App.GetService<ICpuService>();

    private const bool ForceTraining = false;
    private bool _isInitialized;
    private bool _isTdpInitialized;
    private bool _isUndervoltingAvailable;
    private bool _isUndervoltingChecked;

    // Кэшированные значения TDP
    private double _validatedCpuPower = 35.0;
    private bool _isPlatformPc;

    private readonly SafetyLimits _safetyLimits = new();
    private readonly Dictionary<string, ArchitecturePreset> _architecturePresets = [];
    private readonly CodenameGeneration _codenameGeneration = CodenameGeneration.Unknown;

    // Кэш для метрик
    private readonly Dictionary<string, PresetMetrics> _metricsCache = [];
    private readonly Dictionary<string, PresetOptions> _optionsCache = [];

    public OcFinderService()
    {
        InitializeArchitecturePresets();
        _codenameGeneration = Cpu.GetCodenameGeneration();
    }

    /// <summary>
    ///     Ленивая инициализация TDP - вызывается только при первом обращении
    /// </summary>
    public void LazyInitTdp()
    {
        if (_isTdpInitialized)
        {
            return;
        }

        var cpuPower = Cpu.IsAvailable ? SendSmuCommand.ReturnCpuPowerLimit() : -1;
        CheckUndervoltingFeature();
        var powerTable = _dataProvider?.GetPowerTable();
        var powerTableCheckError = false;
        var checkupCpuPower = 35d;

        if (powerTable is { Length: > 3 })
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

        if (!powerTableCheckError && cpuPower > checkupCpuPower || cpuPower < 0)
        {
            cpuPower = checkupCpuPower;
        }

        _isPlatformPc = Cpu.IsPlatformPc() == true;

        if (cpuPower <= 0)
        {
            cpuPower = 35;
        }

        _validatedCpuPower = cpuPower;

        // Ограничение для мобильных платформ
        if (_validatedCpuPower > 45 && !_isPlatformPc)
        {
            _validatedCpuPower = 45d;
        }

        if (_codenameGeneration == CodenameGeneration.FP4)
        {
            _validatedCpuPower = 35d;
        }

        _isTdpInitialized = true;
    }

    /// <summary>
    ///     Проверка доступности андервольтинга
    /// </summary>
    private bool CheckUndervoltingFeature()
    {
        if (_isUndervoltingChecked)
        {
            return _isUndervoltingAvailable;
        }
        if (!Cpu.IsAvailable)
        {
            return false;
        }

        _isUndervoltingAvailable = SendSmuCommand.ReturnUndervoltingAvailability();
        _isUndervoltingChecked = true;
        return _isUndervoltingAvailable;
    }

    /// <summary>
    ///     Инициализация пресетов архитектур с кривыми для каждого типа пресета
    /// </summary>
    private void InitializeArchitecturePresets()
    {
        // Базовый пресет (используется для всех архитектур как основа)
        var basePreset = new ArchitecturePreset
        {
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

        // PreZen пресет
        _architecturePresets["PreZen"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 0.9,
            ThermalMultiplier = 0.95,
            StapmBonus = 2.0, // Завышаеи мощность для старой архитектуры
            FastBonus = 1.0
        };

        // Zen пресет
        _architecturePresets["Zen"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 1.0,
            ThermalMultiplier = 1.0
        };

        // Zen2 пресет
        _architecturePresets["Zen2"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 1.05,
            ThermalMultiplier = 1.0,
            StapmBonus = 1.0
        };

        // Zen3 пресет (базовый)
        _architecturePresets["Zen3"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 1.05,
            ThermalMultiplier = 1.0,
            StapmBonus = 1.5
        };

        // Zen4 пресет (более эффективный)
        _architecturePresets["Zen4"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 1.15,
            ThermalMultiplier = 1.10,
            StapmBonus = 2.5,
            FastBonus = 1.0
        };

        // Zen5 пресет (самый эффективный)
        _architecturePresets["Zen5"] = new ArchitecturePreset
        {
            LaptopCurves = basePreset.LaptopCurves,
            DesktopCurves = basePreset.DesktopCurves,
            EfficiencyMultiplier = 1.25,
            ThermalMultiplier = 1.20,
            StapmBonus = 3.5,
            FastBonus = 2.0
        };
    }

    /// <summary>
    ///     Получение пресета архитектуры на основе CPU
    /// </summary>
    private ArchitecturePreset GetArchitecturePreset()
    {
        return _codenameGeneration switch
        {
            CodenameGeneration.FP4 => _architecturePresets["PreZen"],
            CodenameGeneration.FP5 => _architecturePresets["Zen"],
            CodenameGeneration.FF3 => _architecturePresets["Zen2"],
            CodenameGeneration.FP6 => _architecturePresets["Zen3"],
            CodenameGeneration.FP7 => _architecturePresets["Zen4"],
            CodenameGeneration.FP8 => _architecturePresets["Zen5"],
            CodenameGeneration.AM4_V1 => _architecturePresets["Zen2"],
            CodenameGeneration.AM4_V2 => _architecturePresets["Zen3"],
            CodenameGeneration.AM5 => _architecturePresets["Zen"],
            _ => _architecturePresets["Zen3"]
        };
    }

    /// <summary>
    ///     Расчет полиномиальной аппроксимации
    /// </summary>
    private static double CalculatePolynomial(double[] coefficients, double x) =>
        coefficients.Select((t, i) => t * Math.Pow(x, coefficients.Length - 1 - i)).Sum();

    /// <summary>
    ///     Получение кривой для конкретного типа пресета
    /// </summary>
    private static double[] GetCurveForPresetType(PresetType type, PresetCurves curves)
    {
        return type switch
        {
            PresetType.Min => curves.MinCurve,
            PresetType.Eco => curves.EcoCurve,
            PresetType.Balance => curves.BalanceCurve,
            PresetType.Speed => curves.PerformanceCurve,
            PresetType.Max => curves.MaxCurve,
            _ => curves.BalanceCurve
        };
    }

    /// <summary>
    ///     Создание пресета с использованием специфичных кривых для каждого типа
    /// </summary>
    public PresetConfiguration CreatePreset(PresetType type, OptimizationLevel level)
    {
        LazyInitTdp();

        var preset = GetArchitecturePreset();
        var curves = _isPlatformPc ? preset.DesktopCurves : preset.LaptopCurves;

        // Получаем кривую для конкретного типа пресета
        var presetCurve = GetCurveForPresetType(type, curves);

        // Рассчитываем STAPM напрямую из кривой пресета
        var stapmValue = CalculatePolynomial(presetCurve, _validatedCpuPower);

        // Добавляем архитектурный бонус
        stapmValue += preset.StapmBonus;

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
        var fastValue = _isPlatformPc
            ? stapmValue + preset.FastBonus
            : FromValueToUpper(curves.FastMultipliers[0] * stapmValue + curves.FastMultipliers[1] + preset.FastBonus,
                3);

        // Температурные лимиты
        var tempLimit = level switch
        {
            OptimizationLevel.Basic => Math.Min(ArchitecturePreset.MaxSafeTempBasic, GetBaseTempForPreset(type)),
            OptimizationLevel.Standard => Math.Min(ArchitecturePreset.MaxSafeTempStandard, GetBaseTempForPreset(type)),
            OptimizationLevel.Deep => Math.Min(ArchitecturePreset.MaxSafeTempDeep, GetBaseTempForPreset(type)),
            _ => GetBaseTempForPreset(type)
        };

        // Временные параметры
        var (stapmTime, slowTime, prochotRamp) = GetTimingParameters(type, level);

        var commandString =
            BuildCommandString(stapmValue, fastValue, tempLimit, stapmTime, slowTime, prochotRamp, level);

        return new PresetConfiguration
        {
            Type = type,
            Level = level,
            IsUndervoltingEnabled = level == OptimizationLevel.Deep && _isUndervoltingAvailable,
            CommandString = commandString,
            Metrics = CalculateMetrics(type, level, stapmValue, tempLimit, preset),
            Options = GetPresetOptions(commandString)
        };
    }

    private static int GetBaseTempForPreset(PresetType type) => type switch
    {
        PresetType.Min => 60,
        PresetType.Eco => 70,
        PresetType.Balance => 90,
        PresetType.Speed => 90,
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
            PresetType.Speed => (200, 3, 200),
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

    private string BuildCommandString(double stapm, double fast, int tempLimit, int stapmTime, int slowTime,
        int prochotRamp, OptimizationLevel level)
    {
        var sb = new StringBuilder();

        sb.Append($"--fast-limit={(int)(fast * 1000)} ");

        if (_codenameGeneration != CodenameGeneration.FP4)
        {
            sb.Append($"--tctl-temp={tempLimit} ");

            // DragonRange is laptop CPU but with Desktop silicon and has Stapm limit
            if ((_codenameGeneration == CodenameGeneration.AM5 && Cpu.IsDragonRange) || _codenameGeneration != CodenameGeneration.AM5)
            {
                sb.Append($"--stapm-limit={(int)(stapm * 1000)} ");
            }

            sb.Append($"--slow-limit={(int)(stapm * 1000)} ");
            sb.Append($"--stapm-time={stapmTime} ");
            sb.Append($"--slow-time={slowTime} ");
            sb.Append("--vrm-current=120000 ");
            sb.Append("--vrmmax-current=140000 ");
            sb.Append("--vrmsoc-current=100000 ");
            sb.Append("--vrmsocmax-current=110000 ");
        }
        else
        {
            var tempLimitBr = tempLimit > 85 ? 84000 : tempLimit * 1000;
            sb.Append($"--tctl-temp={tempLimitBr} ");
            sb.Append($"--stapm-limit={(int)(stapm * 1000)},2,{stapmTime * 1000} ");
            sb.Append("--max-performance=0 "); // Включит Max Speed режим
            sb.Append("--disable-feature=10 "); // Выключит Pkg-Pwr лимит

            if (level == OptimizationLevel.Deep)
            {
                sb.Append("--disable-feature=8 "); // Выключит TDC лимит
            }
        }

        sb.Append($"--prochot-deassertion-ramp={prochotRamp} ");

        // Для глубокой оптимизации добавляем андервольтинг если доступен
        if (level == OptimizationLevel.Deep && _isUndervoltingAvailable)
        {
            if (AppSettings.PremadeCurveOptimizerOverrideLevel is <= -51 or >= 1)
            {
                AppSettings.PremadeCurveOptimizerOverrideLevel = -10;
                AppSettings.SaveSettings();
            }

            sb.Append(CurveOptimizerGenerateStringHelper(AppSettings
                .PremadeCurveOptimizerOverrideLevel)); // Андервольтинг
        }

        return sb.ToString().Trim();
    }

    public string CurveOptimizerGenerateStringHelper(int value) =>
        value >= 0
            ? $" --set-coall={value} "
            : $" --set-coall={0x100000U - (uint)-value} ";

    private PresetMetrics CalculateMetrics(PresetType type, OptimizationLevel level, double stapm, int tempLimit,
        ArchitecturePreset preset)
    {
        var cacheKey = $"{type}_{level}_{stapm:F1}_{tempLimit}";
        if (_metricsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Константы для настройки
        const double baseTemp = 85.0; // Базовая температура для расчетов
        const double
            performanceCurvePower = 0.45; // Степень для кривой производительности (меньше 0.5 = более пологая кривая)
        const double efficiencySweetSpot = 0.8; // Точка максимальной энергоэффективности (75% от базовой мощности)

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
            performanceBase = Math.Pow(boost, performanceCurvePower) * 50;
        }
        else
        {
            // Эко режим: более резкое падение производительности
            var reduction = 1.0 - powerRatio;
            performanceBase = -Math.Pow(reduction, performanceCurvePower * 0.8) * 50;
        }

        // === ТЕМПЕРАТУРЫ ===
        // Чем ниже лимит температуры, тем лучше для системы
        var tempRatio = tempLimit / baseTemp;
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

        // Применяем термальный множитель пресета
        thermalScore *= preset.ThermalMultiplier;

        // === ЭНЕРГОЭФФЕКТИВНОСТЬ ===

        // Расстояние от оптимальной точки
        var distanceFromSweet = Math.Abs(powerRatio - efficiencySweetSpot);

        // Базовая эффективность - колоколообразная кривая с центром в efficiencySweetSpot
        var efficiencyBase = 50 * Math.Exp(-Math.Pow(distanceFromSweet * 2, 2));

        // Корректировка на основе EfficiencyMultiplier
        // Высокий multiplier (1.25) = процессор эффективен на низких мощностях
        // Низкий multiplier (0.8) = процессор менее эффективен, но лучше масштабируется
        if (preset.EfficiencyMultiplier > 1.0)
        {
            // Эффективный процессор: лучше работает на низких мощностях
            if (powerRatio < 1.0)
            {
                efficiencyBase *= preset.EfficiencyMultiplier;
            }
            else
            {
                // Меньше выигрыша от повышения мощности
                efficiencyBase *= 2.0 - preset.EfficiencyMultiplier;
            }
        }
        else
        {
            // Менее эффективный процессор: лучше масштабируется с мощностью
            if (powerRatio > 1.0)
            {
                // Больше выигрыша от повышения мощности
                efficiencyBase *= 1.0 + (1.0 - preset.EfficiencyMultiplier);
            }
            else
            {
                efficiencyBase *= preset.EfficiencyMultiplier;
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
    public bool IsUndervoltingAvailable() => CheckUndervoltingFeature();

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

        var options = new PresetOptions
        {
            ThermalOptions = values.GetValueOrDefault("--tctl-temp", 80) + "C",
            PowerOptions = new[]
                {
                    values.GetValueOrDefault("--stapm-limit", 0),
                    values.GetValueOrDefault("--fast-limit", 0),
                    values.GetValueOrDefault("--slow-limit", 0)
                }.Select(v => (v / 1000) + "W")
                .Where(s => s != "0W").Distinct()
                .Aggregate("", (a, b) => string.IsNullOrEmpty(a) ? b : a + ", " + b),
            CurrentOptions = (values.GetValueOrDefault("--vrmmax-current", 0) / 1000) + "A, " +
                             (values.GetValueOrDefault("--vrm-current", 0) / 1000) + "A, " +
                             (values.GetValueOrDefault("--vrmsocmax-current", 0) / 1000) + "A"
        };

        _optionsCache[cacheKey] = options;

        return options;
    }

    public void ClearMetricsCache() => _metricsCache.Clear();

    private static int FromValueToUpper(double value, int upper) => (int)Math.Ceiling(value / upper) * upper;

    // Legacy методы для совместимости
    public void GeneratePremadePresets()
    {
        if (_isInitialized && !ForceTraining)
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
            GeneratePremadePresets();
        }

        // Генерируем данные на основе Balance и Speed пресетов
        var balancePreset = CreatePreset(PresetType.Balance, OptimizationLevel.Standard);
        var performancePreset = CreatePreset(PresetType.Speed, OptimizationLevel.Standard);

        // Извлекаем значения из строк команд (упрощенно)
        var balanceValues = ParseCommandString(balancePreset.CommandString);
        var performanceValues = ParseCommandString(performancePreset.CommandString);

        return new ValueTuple<int[], int[], int[], int[], int[], int[], int[]>(
            [balanceValues.TempLimit, performanceValues.TempLimit],
            [balanceValues.StapmLimit, performanceValues.StapmLimit],
            [balanceValues.FastLimit, performanceValues.FastLimit],
            [balanceValues.SlowLimit, performanceValues.SlowLimit],
            [balanceValues.SlowTime, performanceValues.SlowTime],
            [balanceValues.StapmTime, performanceValues.StapmTime],
            [balanceValues.ProchotRamp, performanceValues.ProchotRamp]
        );
    }

    private static (int TempLimit, int StapmLimit, int FastLimit, int SlowLimit, int SlowTime, int StapmTime, int
        ProchotRamp) ParseCommandString(string commandString)
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