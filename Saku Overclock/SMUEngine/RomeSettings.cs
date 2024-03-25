namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class RomeSettings : SMU
{
    public RomeSettings()
    {
        SMU_TYPE = SmuType.TYPE_CPU2;
        Rsmu.SMU_ADDR_MSG = 61932836U;
        Rsmu.SMU_ADDR_RSP = 61932912U;
        Rsmu.SMU_ADDR_ARG = 61934144U;
        Rsmu.SMU_MSG_TransferTableToDram = 5U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 6U;
        Rsmu.SMU_MSG_GetTableVersion = 8U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 24U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = 18U;
        Mp1Smu.SMU_ADDR_MSG = 61932848U;
        Mp1Smu.SMU_ADDR_RSP = 61932924U;
        Mp1Smu.SMU_ADDR_ARG = 61934020U;
    }
}