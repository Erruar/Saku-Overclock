using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Services;

public class CompositeDataProvider : IDataProvider
{
    private readonly RyzenadjProvider _ryzenadjProvider;
    private readonly ZenstatesCoreProvider _zenstatesProvider;
    private bool _fallbackMode = false;

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

    /// <summary>
    /// Возвращает информацию с использованием Ryzenadj, если он доступен,
    /// иначе – через Zenstates Core.
    /// </summary>
    public async Task<SensorsInformation> GetDataAsync()
    {
        if (!_fallbackMode)
        {
            try
            {
                return await _ryzenadjProvider.GetDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении данных через Ryzenadj: {ex.Message}");
                _fallbackMode = true;
            }
        }

        // Если мы в режиме fallback – используем Zenstates Core.
        return await _zenstatesProvider.GetDataAsync();
    }
}
