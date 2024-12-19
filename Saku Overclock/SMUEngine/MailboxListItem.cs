namespace Saku_Overclock.SMUEngine;

/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class MailboxListItem
{
    public uint MsgAddr
    {
        get;
    }

    public uint RspAddr
    {
        get;
    }

    public uint ArgAddr
    {
        get;
    }

    private string Label
    {
        get;
    }

    public MailboxListItem(string label, SmuAddressSet addressSet)
    {
        Label = label;
        MsgAddr = addressSet.MsgAddress;
        RspAddr = addressSet.RspAddress;
        ArgAddr = addressSet.ArgAddress;
    }

    public MailboxListItem(string label, ZenStates.Core.Mailbox mailbox)
    {
        Label = label;
        MsgAddr = mailbox.SMU_ADDR_MSG;
        RspAddr = mailbox.SMU_ADDR_RSP;
        ArgAddr = mailbox.SMU_ADDR_ARG;
    }

    public override string ToString()
    {
        return Label;
    }
}