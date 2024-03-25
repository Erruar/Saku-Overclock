namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class APUSettings0 : SMU
{
    public APUSettings0()
    {
        SMU_TYPE = SmuType.TYPE_APU0;
        Rsmu.SMU_ADDR_MSG = 61934112U;
        Rsmu.SMU_ADDR_RSP = 61934208U;
        Rsmu.SMU_ADDR_ARG = 61934216U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 11U;
        Rsmu.SMU_MSG_GetTableVersion = 12U;
        Rsmu.SMU_MSG_TransferTableToDram = 61U;
        Rsmu.SMU_MSG_EnableOcMode = 105U;
        Rsmu.SMU_MSG_DisableOcMode = 106U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 125U;
        Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 126U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = (uint)sbyte.MaxValue;
        Rsmu.SMU_MSG_GetPBOScalar = 104U;
        Mp1Smu.SMU_ADDR_MSG = 61932840U;
        Mp1Smu.SMU_ADDR_RSP = 61932900U;
        Mp1Smu.SMU_ADDR_ARG = 61933976U;
    }
}