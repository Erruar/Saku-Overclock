using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RTSSSharedMemoryNET; 
using System.Windows;

namespace Saku_Overclock.SMUEngine;

public static class RTSSHandler
{
    #region Fields

    private static Process? _rtssInstance;

    private static OSD? _osd;

    #endregion

    #region Properties

    /// <summary>
    /// Path to RTSS
    /// </summary>
    public static string RTSSPath
    {
        get; set;
    }

    /// <summary>
    /// Returns true if RTSS is running
    /// </summary>
    public static bool IsRTSSRunning => Process.GetProcessesByName("RTSS").Length != 0;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the RTSSHandler class
    /// </summary>
    static RTSSHandler()
    {
        // HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Unwinder\RTSS\InstallDir
        RTSSPath = @"C:\Program Files (x86)\RivaTuner Statistics Server\RTSS.exe";
    }

    #endregion

    #region Methods

    /// <summary>
    /// Send text to RTSS
    /// </summary>
    /// <param name="text">Text</param>
    public static void Print(string text)
    {
        if (IsRTSSRunning)
        {
            _osd?.Update(text); 
        }
    }

    /// <summary>
    /// Launches RTSS
    /// </summary>
    public static void RunRTSS()
    {
        if (_rtssInstance == null && !IsRTSSRunning && File.Exists(RTSSPath))
        {
            try
            {
                _rtssInstance = Process.Start(RTSSPath);
                Thread.Sleep(2000); // If it works, don't touch it
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Could not start the RTSS", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RunOSD();
        }
        else
        {
            RunOSD();
        }
    }

    /// <summary>
    /// Launches OSD
    /// </summary>
    private static void RunOSD()
    {
        if (_osd == null)
        {
            try
            {
                _osd = new OSD("SHOTOVERRAY");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Could not start the OSD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Closes RTSS
    /// </summary>
    public static void KillRTSS()
    {
        if (_rtssInstance != null)
        {
            try
            {
                _rtssInstance.Kill();
                _rtssInstance = null;
                var proc = Process.GetProcessesByName("RTSSHooksLoader64");
                proc[0].Kill();
            }
            catch (Exception)
            {
                // Ignored
            }
        }
    }

    #endregion
}
