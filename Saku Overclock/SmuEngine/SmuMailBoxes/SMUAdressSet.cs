namespace Saku_Overclock.SmuEngine.SmuMailBoxes;

public class SmuAddressSet(uint msgAddress, uint rspAddress, uint argAddress)
{
    public uint MsgAddress = msgAddress;
    public uint RspAddress = rspAddress;
    public uint ArgAddress = argAddress;
}
