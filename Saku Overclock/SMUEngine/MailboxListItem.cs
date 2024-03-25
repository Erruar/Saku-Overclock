namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class MailboxListItem
{
    public uint msgAddr
    {
        get;
    }
    public uint rspAddr
    {
        get;
    }
    public uint argAddr
    {
        get;
    }
    public string label
    {
        get;
    }

    public MailboxListItem(string label, SmuAddressSet addressSet)
    {
        this.label = label;
        msgAddr = addressSet.MsgAddress;
        rspAddr = addressSet.RspAddress;
        argAddr = addressSet.ArgAddress;
    }

    public MailboxListItem(string label, Mailbox mailbox)
    {
        this.label = label;
        msgAddr = mailbox.SMU_ADDR_MSG;
        rspAddr = mailbox.SMU_ADDR_RSP;
        argAddr = mailbox.SMU_ADDR_ARG;
    }

    public override string ToString()
    {
        return label;
    }
}