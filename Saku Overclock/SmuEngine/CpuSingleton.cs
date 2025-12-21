using ZenStates.Core;

namespace Saku_Overclock.SmuEngine;
public class CpuSingleton
{
    private static readonly Cpu Cpu = new();
    private CpuSingleton()
    {
    }

    public static Cpu GetInstance()
    {
        return Cpu;
    }
}
