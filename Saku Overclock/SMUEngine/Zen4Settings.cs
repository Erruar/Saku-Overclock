namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class Zen4Settings : Zen3Settings
{
    public Zen4Settings()
    {
        SMU_TYPE = SmuType.TYPE_CPU4;
        Mp1Smu.SMU_MSG_SetTDCVDDLimit = 60U;
        Mp1Smu.SMU_MSG_SetEDCVDDLimit = 61U;
        Mp1Smu.SMU_MSG_SetPPTLimit = 62U;
        Mp1Smu.SMU_MSG_SetHTCLimit = 63U;
        Rsmu.SMU_ADDR_MSG = 61932836U;
        Rsmu.SMU_ADDR_RSP = 61932912U;
        Rsmu.SMU_ADDR_ARG = 61934144U;
        Rsmu.SMU_MSG_TransferTableToDram = 3U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 4U;
        Rsmu.SMU_MSG_GetTableVersion = 5U;
        Rsmu.SMU_MSG_EnableOcMode = 93U;
        Rsmu.SMU_MSG_DisableOcMode = 94U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 95U;
        Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 96U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = 97U;
        Rsmu.SMU_MSG_SetPPTLimit = 86U;
        Rsmu.SMU_MSG_SetTDCVDDLimit = 87U;
        Rsmu.SMU_MSG_SetEDCVDDLimit = 88U;
        Rsmu.SMU_MSG_SetHTCLimit = 89U;
        Rsmu.SMU_MSG_SetPBOScalar = 91U;
        Rsmu.SMU_MSG_GetPBOScalar = 109U;
        Rsmu.SMU_MSG_SetDldoPsmMargin = 6U;
        Rsmu.SMU_MSG_SetAllDldoPsmMargin = 7U;
        Rsmu.SMU_MSG_GetDldoPsmMargin = 213U;
        Rsmu.SMU_MSG_GetLN2Mode = 221U;
        Hsmp.SMU_ADDR_MSG = 61932852U;
        Hsmp.SMU_ADDR_RSP = 61933952U;
        Hsmp.SMU_ADDR_ARG = 61934048U;
    }
}