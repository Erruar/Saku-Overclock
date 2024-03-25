namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public sealed class MP1Mailbox : Mailbox
{
    public uint SMU_MSG_SetToolsDramAddress { get; set; }

    public uint SMU_MSG_EnableOcMode { get; set; }

    public uint SMU_MSG_DisableOcMode { get; set; }

    public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; }

    public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; }

    public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0;

    public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0;

    public uint SMU_MSG_SetOverclockCpuVid { get; set; }

    public uint SMU_MSG_SetDldoPsmMargin { get; set; }

    public uint SMU_MSG_SetAllDldoPsmMargin { get; set; }

    public uint SMU_MSG_GetDldoPsmMargin { get; set; }

    public uint SMU_MSG_SetPBOScalar { get; set; }

    public uint SMU_MSG_SetEDCVDDLimit { get; set; }

    public uint SMU_MSG_SetTDCVDDLimit { get; set; }

    public uint SMU_MSG_SetPPTLimit { get; set; }

    public uint SMU_MSG_SetHTCLimit { get; set; }
}