namespace Saku_Overclock.SmuEngine.SmuMailBoxes;

public class MailboxListItem(string label, SmuAddressSet addressSet)
{
    public uint MsgAddr
    {
        get;
    } = addressSet.MsgAddress;

    public uint RspAddr
    {
        get;
    } = addressSet.RspAddress;

    public uint ArgAddr
    {
        get;
    } = addressSet.ArgAddress;

    private string Label
    {
        get;
    } = label;

    public override string ToString()
    {
        return Label;
    }
}