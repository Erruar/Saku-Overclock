using ZenStates.Core;

namespace Saku_Overclock.Contracts.Services;

public interface ISendSmuCommandService
{
    bool GetSetSafeReapply(bool? value = null);
    void ApplyQuickSmuCommand(bool startup);
    void Translate(string ryzenAdjString, bool save);
    void CancelRange();
    void SendRange(string commandIndex, string startIndex, string endIndex, int mailbox, bool log);
    event EventHandler? RangeCompleted;
    string GetCodeNameGeneration();
    uint ReturnCoGfx(bool isMp1);
    uint ReturnCoPer(bool isMp1);
    double ReturnCpuPowerLimit(SMU smu);
    bool ReturnUndervoltingAvailability(SMU smu);
    bool? IsPlatformPc();
}