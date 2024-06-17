using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks; 
using System.Windows;

namespace Saku_Overclock.SMUEngine;

public static class RTSSHandler
{
    public static void ChangeOSDText(string text)
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"SakuRTSSCLI.exe";
        p.StartInfo.Arguments = " --text " + "\"" + text + "\"";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        try { p.Start(); } catch { /*Ignored*/ }
    }
    public static void ResetOSDText()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"SakuRTSSCLI.exe";
        p.StartInfo.Arguments = " --reset ";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        try { p.Start(); } catch { /*Ignored*/ }
    }
}
