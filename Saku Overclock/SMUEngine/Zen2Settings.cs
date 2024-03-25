namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class Zen2Settings : SMU
{
    public Zen2Settings()
    {
        SMU_TYPE = SmuType.TYPE_CPU2;
        Rsmu.SMU_ADDR_MSG = 61932836U;
        Rsmu.SMU_ADDR_RSP = 61932912U;
        Rsmu.SMU_ADDR_ARG = 61934144U;
        Rsmu.SMU_MSG_TransferTableToDram = 5U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 6U;
        Rsmu.SMU_MSG_GetTableVersion = 8U;
        Rsmu.SMU_MSG_EnableOcMode = 90U;
        Rsmu.SMU_MSG_DisableOcMode = 91U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 92U;
        Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 93U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = 97U;
        Rsmu.SMU_MSG_SetPPTLimit = 83U;
        Rsmu.SMU_MSG_SetTDCVDDLimit = 84U;
        Rsmu.SMU_MSG_SetEDCVDDLimit = 85U;
        Rsmu.SMU_MSG_SetHTCLimit = 86U;
        Rsmu.SMU_MSG_GetFastestCoreofSocket = 89U;
        Rsmu.SMU_MSG_SetPBOScalar = 88U;
        Rsmu.SMU_MSG_GetPBOScalar = 108U;
        Rsmu.SMU_MSG_ReadBoostLimit = 110U;
        Mp1Smu.SMU_ADDR_MSG = 61932848U;
        Mp1Smu.SMU_ADDR_RSP = 61932924U;
        Mp1Smu.SMU_ADDR_ARG = 61934020U;
        Mp1Smu.SMU_MSG_SetToolsDramAddress = 6U;
        Mp1Smu.SMU_MSG_EnableOcMode = 36U;
        Mp1Smu.SMU_MSG_DisableOcMode = 37U;
        Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 39U;
        Mp1Smu.SMU_MSG_SetOverclockCpuVid = 40U;
        Mp1Smu.SMU_MSG_SetPBOScalar = 47U;
        Mp1Smu.SMU_MSG_SetEDCVDDLimit = 60U;
        Mp1Smu.SMU_MSG_SetTDCVDDLimit = 59U;
        Mp1Smu.SMU_MSG_SetPPTLimit = 61U;
        Mp1Smu.SMU_MSG_SetHTCLimit = 62U;
        Hsmp.SMU_ADDR_MSG = 61932852U;
        Hsmp.SMU_ADDR_RSP = 61933952U;
        Hsmp.SMU_ADDR_ARG = 61934048U;
    }
}