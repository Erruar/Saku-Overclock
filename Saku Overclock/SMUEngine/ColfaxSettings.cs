namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class ColfaxSettings : SMU
{
    public ColfaxSettings()
    {
        SMU_TYPE = SmuType.TYPE_CPU1;
        Rsmu.SMU_ADDR_MSG = 61932828U;
        Rsmu.SMU_ADDR_RSP = 61932904U;
        Rsmu.SMU_ADDR_ARG = 61932944U;
        Rsmu.SMU_MSG_TransferTableToDram = 10U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 12U;
        Rsmu.SMU_MSG_EnableOcMode = 99U;
        Rsmu.SMU_MSG_DisableOcMode = 100U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 104U;
        Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 105U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = 106U;
        Rsmu.SMU_MSG_SetTDCVDDLimit = 107U;
        Rsmu.SMU_MSG_SetEDCVDDLimit = 108U;
        Rsmu.SMU_MSG_SetHTCLimit = 110U;
        Rsmu.SMU_MSG_SetPBOScalar = 111U;
        Rsmu.SMU_MSG_GetPBOScalar = 112U;
        Mp1Smu.SMU_ADDR_MSG = 61932840U;
        Mp1Smu.SMU_ADDR_RSP = 61932900U;
        Mp1Smu.SMU_ADDR_ARG = 61932952U;
    }
}