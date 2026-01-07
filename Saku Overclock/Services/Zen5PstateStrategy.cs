using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.SmuEngine;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Services;
public class Zen5PstateStrategy : IPstateStrategy
{
    private readonly ICpuService _cpuService;

    private const uint MsrPstateBase = 0xC0010064;
    private const uint MsrHwcr = 0xC0010015;
    private const uint HwcrTscFreqSel = 0x200000;

    public bool IsSupportedFamily
    {
        get;
    }

    public Zen5PstateStrategy(
        ICpuService cpuService)
    {
        _cpuService = cpuService;
        IsSupportedFamily = _cpuService.Family > CpuFamily.Family19H;
    }

    public PstateOperationResult ReadPstate(int stateNumber)
    {
        if (stateNumber < 0 || stateNumber > 2)
        {
            return PstateOperationResult.Fail($"Invalid P-State number: {stateNumber}");
        }

        try
        {
            uint eax = 0, edx = 0;
            var msr = MsrPstateBase + (uint)stateNumber;

            if (!_cpuService.ReadMsr(msr, ref eax, ref edx))
            {
                LogHelper.LogError($"Failed to read MSR 0x{msr:X} for P-State {stateNumber}");
                return PstateOperationResult.Fail("MSR read failed");
            }

            var pstate = ParseZen5Pstate(eax, edx, stateNumber);
            return PstateOperationResult.Ok(pstate);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
            return PstateOperationResult.Fail(ex.Message);
        }
    }

    public PstateOperationResult WritePstate(PstateWriteParams parameters)
    {
        if (!ValidateParameters(parameters, out var error))
        {
            return PstateOperationResult.Fail(error);
        }

        try
        {
            var currentResult = ReadPstate(parameters.StateNumber);
            if (!currentResult.Success)
            {
                return currentResult;
            }

            var current = currentResult.Data;

            // Zen 5: Freq = 5 * Fid
            var fid = (uint)Math.Round(parameters.FrequencyMHz / 5.0);
            fid = Math.Clamp(fid, 100u, 1200u); // Примерный диапазон

            // Zen 5: Voltage(uV) = 5000 * (Vid + 49), Vid = 0..511
            // Vid = Voltage(uV) / 5000 - 49
            var voltageUv = parameters.VoltageMillivolts * 1000.0;
            var vid = (uint)Math.Round(voltageUv / 5000.0 - 49.0);
            vid = Math.Clamp(vid, 0u, 511u);

            var vidLow = vid & 0xFF;
            var vidHigh = (vid >> 8) & 0x1;

            var eax = BuildZen5Eax(fid, vidLow, current.IddValue, current.IddDiv);

            // EDX: bit 1 (в младших 32 битах EDX) = VidHigh, bit 31 = PstateEn
            var edx = vidHigh << 1;
            if (parameters.Enable.HasValue && parameters.Enable.Value)
            {
                edx |= 0x80000000u;
            }
            else if (parameters.Enable.HasValue && !parameters.Enable.Value)
            {
                edx &= ~0x80000000u;
            }
            else if (current.IsEnabled)
            {
                edx |= 0x80000000u;
            }

            if (!ApplyTscWorkaround())
            {
                LogHelper.LogError("Failed to apply TSC workaround");
                return PstateOperationResult.Fail("TSC workaround failed");
            }

            if (!WritePstateToAllNodes(parameters.StateNumber, eax, edx))
            {
                return PstateOperationResult.Fail("Failed to write P-State to all nodes");
            }

            return ReadPstate(parameters.StateNumber);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
            return PstateOperationResult.Fail(ex.Message);
        }
    }

    public bool ValidateParameters(PstateWriteParams parameters, out string? error)
    {
        error = null;

        if (parameters.StateNumber < 0 || parameters.StateNumber > 1)
        {
            error = "P-State number must be 0 or 1";
            return false;
        }

        // Zen 5: Freq = 5 * Fid, Fid = 12 бит (0-4095)
        if (parameters.FrequencyMHz < 500 || parameters.FrequencyMHz > 8000)
        {
            error = "Frequency must be between 500 and 8000 MHz";
            return false;
        }

        // Zen 5: Voltage = 5000 * (Vid + 49) uV, Vid = 0..511
        // Min: 5000 * 49 = 245000 uV = 245 mV
        // Max: 5000 * 560 = 2800000 uV = 2800 mV
        if (parameters.VoltageMillivolts < 245 || parameters.VoltageMillivolts > 1600)
        {
            error = "Voltage must be between 245 and 1600 mV for Zen 5";
            return false;
        }

        return true;
    }

    // ====================================================================
    // Приватные методы для Zen 5
    // ====================================================================

    private static PstateData ParseZen5Pstate(uint eax, uint edx, int stateNumber)
    {
        // Zen 5 структура:
        // EAX[11:0]  = Fid (12 бит)
        // EAX[21:14] = VidLow (8 бит)
        // EAX[29:22] = IddValue
        // EAX[31:30] = IddDiv
        // EDX[1]     = VidHigh (бит 33 в 64-битном MSR становится битом 1 в EDX)
        // EDX[31]    = PstateEn (бит 63 в MSR)

        var fid = eax & 0xFFF;
        var vidLow = (eax >> 14) & 0xFF;
        var iddValue = (eax >> 22) & 0xFF;
        var iddDiv = (eax >> 30) & 0x3;
        var vidHigh = (edx >> 1) & 0x1;
        var isEnabled = (edx & 0x80000000) != 0;

        var vid = vidLow | (vidHigh << 8);

        // Zen 5 формулы:
        // Freq = 5 * Fid (MHz)
        var frequencyMHz = 5.0 * fid;

        // Voltage = 5000 * (Vid + 49) uV
        var voltageUv = 5000.0 * (vid + 49);
        var voltageMillivolts = voltageUv / 1000.0;

        return new PstateData
        {
            StateNumber = stateNumber,
            IsEnabled = isEnabled,
            FrequencyMHz = Math.Round(frequencyMHz, 2),
            VoltageMillivolts = Math.Round(voltageMillivolts, 2),
            Fid = fid,
            Did = 0, // Zen 5 не использует Did
            Vid = vid,
            IddValue = iddValue,
            IddDiv = iddDiv
        };
    }

    private static uint BuildZen5Eax(uint fid, uint vidLow, uint iddValue, uint iddDiv)
    {
        return (fid & 0xFFF) |
               ((vidLow & 0xFF) << 14) |
               ((iddValue & 0xFF) << 22) |
               ((iddDiv & 0x3) << 30);
    }

    private bool ApplyTscWorkaround()
    {
        try
        {
            uint eax = 0, edx = 0;
            if (!_cpuService.ReadMsr(MsrHwcr, ref eax, ref edx))
            {
                LogHelper.LogError("Failed to read HWCR MSR");
                return false;
            }

            eax |= HwcrTscFreqSel;
            return _cpuService.WriteMsr(MsrHwcr, eax, edx);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
            return false;
        }
    }

    private bool WritePstateToAllNodes(int stateNumber, uint eax, uint edx)
    {
        var msr = MsrPstateBase + (uint)stateNumber;

        if (NumaUtil.HighestNumaNode > 0)
        {
            for (var node = 0u; node <= NumaUtil.HighestNumaNode; node++)
            {
                NumaUtil.SetThreadProcessorAffinity(
                    (ushort)(node + 1),
                    [.. Enumerable.Range(0, Environment.ProcessorCount)]
                );

                if (!_cpuService.WriteMsr(msr, eax, edx))
                {
                    LogHelper.LogError($"Failed to write P-State {stateNumber} on NUMA node {node}");
                    return false;
                }
            }
        }
        else
        {
            if (!_cpuService.WriteMsr(msr, eax, edx))
            {
                LogHelper.LogError($"Failed to write P-State {stateNumber}");
                return false;
            }
        }

        return true;
    }
}