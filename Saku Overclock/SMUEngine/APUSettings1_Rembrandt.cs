namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class APUSettings1_Rembrandt : APUSettings1
{
    public APUSettings1_Rembrandt()
    {
        SMU_TYPE = SmuType.TYPE_APU2;
        Rsmu.SMU_MSG_SetPBOScalar = 62U;
        Rsmu.SMU_MSG_SetDldoPsmMargin = 83U;
        Rsmu.SMU_MSG_SetAllDldoPsmMargin = 93U;
        Rsmu.SMU_MSG_GetDldoPsmMargin = 47U;
        Rsmu.SMU_MSG_SetGpuPsmMargin = 183U;
        Rsmu.SMU_MSG_GetGpuPsmMargin = 48U;
        Mp1Smu.SMU_ADDR_MSG = 61932840U;
        Mp1Smu.SMU_ADDR_RSP = 61932920U;
        Mp1Smu.SMU_ADDR_ARG = 61933976U;
    }
}