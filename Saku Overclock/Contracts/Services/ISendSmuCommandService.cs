namespace Saku_Overclock.Contracts.Services;

public interface ISendSmuCommandService
{
    void Translate(string ryzenAdjString, bool save);
    uint ReturnCoPer(bool isMp1 = true);
}