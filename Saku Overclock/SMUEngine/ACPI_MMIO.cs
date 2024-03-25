namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class ACPI_MMIO
{
    internal const uint ACPI_MMIO_BASE_ADDRESS = 4275568640;
    internal const uint MISC_BASE = 4275572224;
    internal const uint MISC_GPPClkCntrl = 4275572224;
    internal const uint MISC_ClkOutputCntrl = 4275572228;
    internal const uint MISC_CGPLLConfig1 = 4275572232;
    internal const uint MISC_CGPLLConfig2 = 4275572236;
    internal const uint MISC_CGPLLConfig3 = 4275572240;
    internal const uint MISC_CGPLLConfig4 = 4275572244;
    internal const uint MISC_CGPLLConfig5 = 4275572248;
    internal const uint MISC_ClkCntl1 = 4275572288;
    internal const uint MISC_StrapStatus = 4275572352;
    private readonly IOModule io;

    public ACPI_MMIO(IOModule io) => this.io = io;

    private static int CalculateBclkIndex(int bclk)
    {
        if (bclk > 151)
        {
            bclk = 151;
        }
        else if (bclk < 96)
        {
            bclk = 96;
        }

        return (bclk & 128) != 0 ? bclk ^ 164 : bclk ^ 100;
    }

    private static int CalculateBclkFromIndex(int index) => index < 32 ? index ^ 100 : index ^ 164;

    public int GetStrapStatus()
    {
        return io.GetPhysLong(4275572352U, out var data) ? (int)Utils.GetBits(data, 17, 1) : -1;
    }

    private bool DisableSpreadSpectrum()
    {
        return io.GetPhysLong(4275572232U, out var data) && io.SetPhysLong(4275572232U, Utils.SetBits(data, 0, 0, 0U));
    }

    private bool CG1AtomicUpdate()
    {
        return io.GetPhysLong(4275572288U, out var data) && io.SetPhysLong(4275572288U, Utils.SetBits(data, 30, 1, 1U));
    }

    public bool SetBclk(double bclk)
    {
        DisableSpreadSpectrum();
        io.GetPhysLong(4275572288U, out var data);
        var flag2 = io.SetPhysLong(4275572288U, Utils.SetBits(data, 25, 1, 1U));
        if (flag2)
        {
            var bclkIndex = CalculateBclkIndex((int)bclk);
            var newVal = (uint)((bclk - (int)bclk) / (1.0 / 16.0));
            if (newVal > 15U)
            {
                newVal = 15U;
            }

            flag2 = io.GetPhysLong(4275572240U, out data);
            if (io.SetPhysLong(4275572240U, Utils.SetBits(Utils.SetBits(data, 4, 9, (uint)bclkIndex), 25, 4, newVal)))
            {
                return CG1AtomicUpdate();
            }
        }
        return flag2;
    }

    public double? GetBclk()
    {
        if (!io.GetPhysLong(4275572240U, out var data))
        {
            return new double?();
        }

        var bits1 = Utils.GetBits(data, 4, 9);
        var bits2 = Utils.GetBits(data, 25, 4);
        return CalculateBclkFromIndex((int)bits1) + bits2 * (1.0 / 16.0);
    }
}