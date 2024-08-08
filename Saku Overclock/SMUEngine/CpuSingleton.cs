using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenStates.Core;

namespace Saku_Overclock.SMUEngine;
public class CpuSingleton
{
    private static readonly Cpu _cpu = new();
    private CpuSingleton()
    {
    }

    public static Cpu GetInstance()
    {
        return _cpu;
    }
}
