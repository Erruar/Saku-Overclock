namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class SMU
{
    protected internal SMU()
    {
        Version = 0U;
        SMU_TYPE = SmuType.TYPE_UNSUPPORTED;
        SMU_PCI_ADDR = 0U;
        SMU_OFFSET_ADDR = 96U;
        SMU_OFFSET_DATA = 100U;
        Rsmu = new RSMUMailbox();
        Mp1Smu = new MP1Mailbox();
        Hsmp = new HSMPMailbox();
    }

    public uint Version
    {
        get; set;
    }

    public uint TableVersion
    {
        get; set;
    }

    public SmuType SMU_TYPE
    {
        get; protected set;
    }

    public uint SMU_PCI_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_ADDR
    {
        get; protected set;
    }

    public uint SMU_OFFSET_DATA
    {
        get; protected set;
    }

    public RSMUMailbox Rsmu
    {
        get; protected set;
    }

    public MP1Mailbox Mp1Smu
    {
        get; protected set;
    }

    public HSMPMailbox Hsmp
    {
        get; protected set;
    }
    [Obsolete]
    private bool SmuWriteReg(uint addr, uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, data);

    [Obsolete]
    private bool SmuReadReg(uint addr, ref uint data) => Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr) && Ring0.ReadPciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, out data);

    [Obsolete]
    private bool SmuWaitDone(Mailbox mailbox)
    {
        ushort num = 8192;
        uint data = 0;
        do
        {
            ;
        }
        while ((!SmuReadReg(mailbox.SMU_ADDR_RSP, ref data) || data == 0U) && --num > 0);
        return num != 0 && data > 0U;
    }

    [Obsolete]
    public Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
    {
        var maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
        {
            return Status.UNKNOWN_CMD;
        }

        if (Ring0.WaitPciBusMutex(10))
        {
            if (!SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return Status.FAILED;
            }
            SmuWriteReg(mailbox.SMU_ADDR_RSP, 0U);
            var numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
            for (var index = 0; index < numArray.Length; ++index)
            {
                SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
            }

            SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
            if (!SmuWaitDone(mailbox))
            {
                Ring0.ReleasePciBusMutex();
                return Status.FAILED;
            }
            SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
            if ((byte)maxValue == 1)
            {
                for (var index = 0; index < args.Length; ++index)
                {
                    SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
                }
            }
            Ring0.ReleasePciBusMutex();
        }
        return (Status)maxValue;
    }

    [Obsolete]
    public void SendSmuCommandNV(Mailbox mailbox, uint msg, ref uint[] args)
    {
        var maxValue = (uint)byte.MaxValue;
        if (msg == 0U || mailbox == null || mailbox.SMU_ADDR_MSG == 0U || mailbox.SMU_ADDR_ARG == 0U || mailbox.SMU_ADDR_RSP == 0U)
        {
            //status = SMU.Status.UNKNOWN_CMD;
            if (Ring0.WaitPciBusMutex(10))
            {
                if (!SmuWaitDone(mailbox: mailbox))
                {
                    Ring0.ReleasePciBusMutex();
                    // status = SMU.Status.FAILED;
                }
                SmuWriteReg(mailbox.SMU_ADDR_RSP, 0U);
                var numArray = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
                for (var index = 0; index < numArray.Length; ++index)
                {
                    SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), numArray[index]);
                }

                SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);
                if (!SmuWaitDone(mailbox))
                {
                    Ring0.ReleasePciBusMutex();
                    //  status = SMU.Status.FAILED;
                }
                SmuReadReg(mailbox.SMU_ADDR_RSP, ref maxValue);
                if ((byte)maxValue == 1)
                {
                    for (var index = 0; index < args.Length; ++index)
                    {
                        SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(index * 4), ref args[index]);
                    }
                }
                Ring0.ReleasePciBusMutex();
            }
        }
        // status = (SMU.Status)maxValue;
    }

    [Obsolete("SendSmuCommand with one argument is deprecated, please use SendSmuCommand with full 6 args")]
    public bool SendSmuCommand(Mailbox mailbox, uint msg, uint arg)
    {
        var args = Utils.MakeCmdArgs(arg, mailbox.MAX_ARGS);
        return SendSmuCommand(mailbox, msg, ref args) == Status.OK;
    }
    [Obsolete]
    public Status SendMp1Command(uint msg, ref uint[] args) => SendSmuCommand(Mp1Smu, msg, ref args);
    [Obsolete]
    public Status SendRsmuCommand(uint msg, ref uint[] args) => SendSmuCommand(Rsmu, msg, ref args);
    [Obsolete]
    public Status SendHsmpCommand(uint msg, ref uint[] args) => Hsmp.IsSupported && msg <= Hsmp.HighestSupportedFunction ? SendSmuCommand(Hsmp, msg, ref args) : Status.UNKNOWN_CMD;

    public enum MailboxType
    {
        UNSUPPORTED,
        RSMU,
        MP1,
        HSMP,
    }

    public enum SmuType
    {
        TYPE_CPU0 = 0,
        TYPE_CPU1 = 1,
        TYPE_CPU2 = 2,
        TYPE_CPU3 = 3,
        TYPE_CPU4 = 4,
        TYPE_CPU9 = 9,
        TYPE_APU0 = 16, // 0x00000010
        TYPE_APU1 = 17, // 0x00000011
        TYPE_APU2 = 18, // 0x00000012
        TYPE_UNSUPPORTED = 255, // 0x000000FF
    }

    public enum Status : byte
    {
        OK = 1,
        CMD_REJECTED_BUSY = 252, // 0xFC
        CMD_REJECTED_PREREQ = 253, // 0xFD
        UNKNOWN_CMD = 254, // 0xFE
        FAILED = 255, // 0xFF
    }
}