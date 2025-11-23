using System.Diagnostics;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Services;

public class CompositeDataProvider : IDataProvider
{
    private readonly RyzenadjProvider _ryzenadjProvider;
    private readonly ZenstatesCoreProvider _zenstatesProvider;
    private bool _fallbackMode;

    public CompositeDataProvider(RyzenadjProvider ryzenadjProvider, ZenstatesCoreProvider zenstatesProvider)
    {
        _ryzenadjProvider = ryzenadjProvider;
        _zenstatesProvider = zenstatesProvider;
        // Пытаемся инициализировать Ryzenadj.
        _ryzenadjProvider.Initialize();
        // Если после инициализации указатель равен IntPtr.Zero, переключаемся в fallback.
        if (RyzenadjProvider.IsPhysicallyUnavailable)
        {
            _fallbackMode = true;
        }
    }

    public float[]? GetPowerTable()
    {
        if (!_fallbackMode)
        {
            try
            {
                return _ryzenadjProvider.GetPowerTable();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Ошибка при получении данных через Ryzenadj: {ex.Message}");
                _fallbackMode = true;
            }
        }

        // Если мы в режиме fallback – используем Zenstates Core.
        return _zenstatesProvider.GetPowerTable();
    }

    /// <summary>
    ///     Возвращает информацию с использованием Ryzenadj, если он доступен,
    ///     иначе – через Zenstates Core.
    /// </summary>
    public void GetData(ref SensorsInformation sensorsInformation)
    {
        if (!_fallbackMode)
        {
            try
            {
                _ryzenadjProvider.GetData(ref sensorsInformation);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Ошибка при получении данных через Ryzenadj: {ex.Message}");
                _fallbackMode = true;
            }
            return;
        }

        // Если мы в режиме fallback – используем Zenstates Core.
        _zenstatesProvider.GetData(ref sensorsInformation);
    }
}