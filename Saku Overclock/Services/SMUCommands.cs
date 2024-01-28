using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenStates.Core;

namespace Saku_Overclock.Services;
internal class SMUCommands
{
}

internal abstract class BaseSMUCommand : IDisposable
{
    internal SMU smu;

    internal CmdResult result;

    private bool disposedValue;

    protected BaseSMUCommand(SMU smuInstance, int maxArgs = 6)
    {
        if (smuInstance != null)
        {
            smu = smuInstance;
        }
        result = new CmdResult(maxArgs);
    }

    public virtual bool CanExecute()
    {
        return smu != null;
    }

    public virtual CmdResult Execute()
    {
        if (!result.Success)
        {
            result.args = Utils.MakeCmdArgs();
        }
        Dispose();
        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                smu = null;
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
internal class CmdResult
{
    public SMU.Status status;

    public uint[] args;

    public bool Success => status == SMU.Status.OK;

    public CmdResult(int maxArgs)
    {
        args = Utils.MakeCmdArgs(0u, maxArgs);
        status = SMU.Status.FAILED;
    }
}
internal class GetDramAddress : BaseSMUCommand
{
    public GetDramAddress(SMU smu)
        : base(smu)
    {
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            switch (smu.SMU_TYPE)
            {
                case SMU.SmuType.TYPE_CPU0:
                case SMU.SmuType.TYPE_CPU1:
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                    if (!result.Success)
                    {
                        break;
                    }
                    result.args = Utils.MakeCmdArgs();
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                    if (result.Success)
                    {
                        uint arg = result.args[0];
                        result.args = Utils.MakeCmdArgs();
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress + 2, ref result.args);
                        if (result.Success)
                        {
                            result.args = Utils.MakeCmdArgs(arg);
                        }
                    }
                    break;
                case SMU.SmuType.TYPE_CPU2:
                case SMU.SmuType.TYPE_CPU3:
                case SMU.SmuType.TYPE_CPU4:
                case SMU.SmuType.TYPE_APU1:
                case SMU.SmuType.TYPE_APU2:
                    result.args = Utils.MakeCmdArgs(new uint[2] { 1u, 1u });
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                    break;
                case SMU.SmuType.TYPE_APU0:
                    {
                        uint[] array = new uint[2];
                        result.args = Utils.MakeCmdArgs(3u);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                        if (!result.Success)
                        {
                            break;
                        }
                        result.args = Utils.MakeCmdArgs(3u);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                        if (!result.Success)
                        {
                            break;
                        }
                        array[0] = result.args[0];
                        result.args = Utils.MakeCmdArgs(5u);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                        if (result.Success)
                        {
                            result.args = Utils.MakeCmdArgs(5u);
                            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                            if (result.Success)
                            {
                                array[1] = result.args[0];
                                result.args = Utils.MakeCmdArgs(new uint[2]
                                {
                            array[0],
                            array[1]
                                });
                            }
                        }
                        break;
                    }
            }
        }
        return base.Execute();
    }
}
internal class GetLN2Mode : BaseSMUCommand
{
    public GetLN2Mode(SMU smu)
        : base(smu)
    {
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetLN2Mode, ref result.args);
        }
        return base.Execute();
    }
}
internal class GetPBOScalar : BaseSMUCommand
{
    public float Scalar
    {
        get; protected set;
    }

    public GetPBOScalar(SMU smu)
        : base(smu)
    {
        Scalar = 0f;
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPBOScalar, ref result.args);
            if (result.Success)
            {
                byte[] bytes = BitConverter.GetBytes(result.args[0]);
                Scalar = BitConverter.ToSingle(bytes, 0);
            }
        }
        return base.Execute();
    }
}
internal class GetPsmMarginSingleCore : BaseSMUCommand
{
    public GetPsmMarginSingleCore(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint coreMask)
    {
        if (CanExecute())
        {
            result.args[0] = coreMask & 0xFFF00000u;
            if (smu.Rsmu.SMU_MSG_GetDldoPsmMargin != 0)
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDldoPsmMargin, ref result.args);
            }
            else
            {
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_GetDldoPsmMargin, ref result.args);
            }
        }
        return base.Execute();
    }
}
internal class GetSmuVersion : BaseSMUCommand
{
    public GetSmuVersion(SMU smu)
        : base(smu)
    {
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetSmuVersion, ref result.args);
        }
        return base.Execute();
    }
}
internal class GetTableVersion : BaseSMUCommand
{
    public GetTableVersion(SMU smu)
        : base(smu)
    {
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetTableVersion, ref result.args);
        }
        return base.Execute();
    }
}
internal class SendTestMessage : BaseSMUCommand
{
    private readonly Mailbox mbox;

    public bool IsSumCorrect = false;

    public SendTestMessage(SMU smu, Mailbox mbox = null)
        : base(smu)
    {
        this.mbox = mbox ?? smu.Rsmu;
    }

    public CmdResult Execute(uint testArg = 1u)
    {
        if (CanExecute())
        {
            result.args[0] = testArg;
            result.status = smu.SendSmuCommand(mbox, mbox.SMU_MSG_TestMessage, ref result.args);
            IsSumCorrect = result.args[0] == testArg + 1;
        }
        return base.Execute();
    }
}
internal class SetFrequencyAllCore : BaseSMUCommand
{
    public SetFrequencyAllCore(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint frequency)
    {
        if (CanExecute())
        {
            result.args[0] = frequency & 0xFFFFFu;
            uint sMU_MSG_SetOverclockFrequencyAllCores = smu.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores;
            if (sMU_MSG_SetOverclockFrequencyAllCores != 0)
            {
                result.status = smu.SendRsmuCommand(sMU_MSG_SetOverclockFrequencyAllCores, ref result.args);
            }
            else
            {
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args);
            }
        }
        return base.Execute();
    }
}
internal class SetFrequencySingleCore : BaseSMUCommand
{
    public SetFrequencySingleCore(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint coreMask, uint frequency)
    {
        if (CanExecute())
        {
            result.args[0] = coreMask | (frequency & 0xFFFFFu);
            uint sMU_MSG_SetOverclockFrequencyPerCore = smu.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore;
            if (sMU_MSG_SetOverclockFrequencyPerCore != 0)
            {
                result.status = smu.SendRsmuCommand(sMU_MSG_SetOverclockFrequencyPerCore, ref result.args);
            }
            else
            {
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore, ref result.args);
            }
        }
        return base.Execute();
    }
}
internal class SetOcMode : BaseSMUCommand
{
    public SetOcMode(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(bool enabled, uint arg = 0u)
    {
        if (CanExecute())
        {
            uint num = (enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode);
            result.args[0] = arg;
            if (num != 0)
            {
                result.status = smu.SendRsmuCommand(num, ref result.args);
            }
            else
            {
                result.status = smu.SendMp1Command(enabled ? smu.Mp1Smu.SMU_MSG_EnableOcMode : smu.Mp1Smu.SMU_MSG_DisableOcMode, ref result.args);
            }
            if (!enabled && result.Success)
            {
                result = new SetPBOScalar(smu).Execute();
            }
        }
        return base.Execute();
    }
}
internal class SetOverclockCpuVid : BaseSMUCommand
{
    public SetOverclockCpuVid(SMU smuInstance)
        : base(smuInstance)
    {
    }

    public CmdResult Execute(byte vid)
    {
        if (CanExecute())
        {
            result.args[0] = Convert.ToUInt32(vid);
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockCpuVid, ref result.args);
        }
        return base.Execute();
    }
}
internal class SetPBOScalar : BaseSMUCommand
{
    public SetPBOScalar(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint arg = 1u)
    {
        if (CanExecute())
        {
            result.args[0] = arg * 100;
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetPBOScalar, ref result.args);
        }
        return base.Execute();
    }
}
internal class SetPsmMarginAllCores : BaseSMUCommand
{
    public SetPsmMarginAllCores(SMU smu)
        : base(smu)
    {
    }

    public override bool CanExecute()
    {
        return smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin != 0 || smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin != 0;
    }

    public CmdResult Execute(int margin)
    {
        if (CanExecute())
        {
            result.args[0] = Utils.MakePsmMarginArg(margin);
            if (smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin != 0)
            {
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
            }
            else
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
            }
        }
        return base.Execute();
    }
}
internal class SetPsmMarginSingleCore : BaseSMUCommand
{
    public SetPsmMarginSingleCore(SMU smu)
        : base(smu)
    {
    }

    public override bool CanExecute()
    {
        return smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin != 0 || smu.Rsmu.SMU_MSG_SetDldoPsmMargin != 0;
    }

    public CmdResult Execute(uint coreMask, int margin)
    {
        if (CanExecute())
        {
            uint num = Utils.MakePsmMarginArg(margin);
            result.args[0] = (coreMask & 0xFFF00000u) | num;
            if (smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin != 0)
            {
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin, ref result.args);
            }
            else
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetDldoPsmMargin, ref result.args);
            }
        }
        return base.Execute();
    }
}
internal class SetSmuLimit : BaseSMUCommand
{
    public SetSmuLimit(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint cmd, uint arg = 0u)
    {
        if (CanExecute())
        {
            result.args[0] = arg * 1000;
            result.status = smu.SendRsmuCommand(cmd, ref result.args);
        }
        return base.Execute();
    }
}
internal class SetToolsDramAddress : BaseSMUCommand
{
    public SetToolsDramAddress(SMU smu)
        : base(smu)
    {
    }

    public CmdResult Execute(uint arg = 0u)
    {
        if (CanExecute())
        {
            result.args[0] = arg;
            result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetToolsDramAddress, ref result.args);
        }
        return base.Execute();
    }
}
internal class TransferTableToDram : BaseSMUCommand
{
    public TransferTableToDram(SMU smu)
        : base(smu)
    {
    }

    public override CmdResult Execute()
    {
        if (CanExecute())
        {
            if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU1)
            {
                result.args[0] = 3u;
            }
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_TransferTableToDram, ref result.args);
        }
        return base.Execute();
    }
}
