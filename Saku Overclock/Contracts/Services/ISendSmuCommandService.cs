using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Contracts.Services;

public interface ISendSmuCommandService
{
    bool SafeReapply
    {
        set;
    }
    void ApplyQuickSmuCommand(bool startup);
    void Translate(string ryzenAdjString, bool save);
    void CancelRange();
    void SendRange(string commandIndex, string startIndex, string endIndex, int mailbox, bool log);
    event EventHandler? RangeCompleted;
    CodenameGeneration GetCodeNameGeneration();
    uint ReturnCoPer(bool isMp1 = true);
    double ReturnCpuPowerLimit();
    bool ReturnUndervoltingAvailability();
}