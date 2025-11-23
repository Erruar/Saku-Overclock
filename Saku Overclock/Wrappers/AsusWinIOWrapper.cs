using System.Runtime.InteropServices;

namespace Saku_Overclock.Wrappers;

public static class AsusWinIoWrapper
{
    private const string DllName = "AWinIo64.dll";

    private static bool _isDllRunning;
    /*Included AsusWinIO64.dll is licenced to (c) ASUSTek COMPUTER INC*/
    /*ASUS System Control Interface is necessary for this software to work - ASUS System Analysis service must be running. It's automatically installed with MyASUS app.*/

    #region DLL Voids

    public static void Init_WinIo()
    {
        if (_isDllRunning)
        {
            return;
        }

        InitializeWinIo();
        _isDllRunning = true;
    }

    public static void Cleanup_WinIo()
    {
        if (!_isDllRunning)
        {
            return;
        }

        ShutdownWinIo();
        _isDllRunning = false;
    }

    #endregion

    #region DLL Imports

    /// <summary>
    ///     Инициализация dll
    /// </summary>
    [DllImport(DllName)]
    private static extern void InitializeWinIo();

    /// <summary>
    ///     Освобождение dll
    /// </summary>
    [DllImport(DllName)]
    private static extern void ShutdownWinIo();

    /// <summary>
    ///     Узнать количество кулеров
    /// </summary>
    [DllImport(DllName)]
    public static extern int HealthyTable_FanCounts();

    /// <summary>
    ///     Установить индекс текущего кулера для управления установленным кулером
    /// </summary>
    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanIndex(byte index);

    /// <summary>
    ///     Установть скорость кулера
    /// </summary>
    [DllImport(DllName)]
    public static extern int HealthyTable_FanRPM();

    /// <summary>
    ///     Установить тестовый, диагностический режим
    /// </summary>
    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanTestMode(char mode);

    /// <summary>
    ///     Установить минимальную скорость
    /// </summary>
    [DllImport(DllName)]
    public static extern void HealthyTable_SetFanPwmDuty(short duty);

    #endregion
}