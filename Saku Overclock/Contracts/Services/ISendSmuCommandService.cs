using ZenStates.Core;

namespace Saku_Overclock.Contracts.Services;

public interface ISendSmuCommandService
{
    void Init(Cpu? cpu = null);
    void SetCpuCodename(Cpu.CodeName codename);
    bool GetSetSafeReapply(bool? value = null);
    void Play_Invernate_QuickSMU(int mode);
    void Translate(string ryzenAdjString, bool save);
    void CancelRange();
    void SendRange(string commandIndex, string startIndex, string endIndex, int mailbox, bool log);
    event EventHandler? RangeCompleted;
    string GetCodeNameGeneration(Cpu cpu);
    uint ReturnCoGfx(Cpu.CodeName codeName, bool isMp1);
    uint ReturnCoPer(Cpu.CodeName codeName, bool isMp1);
    double ReturnCpuPowerLimit(Cpu cpu);
    bool ReturnUndervoltingAvailability(Cpu cpu);
    bool? IsPlatformPc(Cpu cpu);

    // ReSharper disable once UnusedMember.Global
    uint GenerateSmuArgForSetGfxclkOverdriveByFreqVid(double frequencyMHz, double voltage);
}