namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class APUSettings1 : SMU
{
    public APUSettings1()
    {
        SMU_TYPE = SmuType.TYPE_APU1;
        Rsmu.SMU_ADDR_MSG = 61934112U;
        Rsmu.SMU_ADDR_RSP = 61934208U;
        Rsmu.SMU_ADDR_ARG = 61934216U;
        Rsmu.SMU_MSG_GetTableVersion = 6U;
        Rsmu.SMU_MSG_TransferTableToDram = 101U;
        Rsmu.SMU_MSG_GetDramBaseAddress = 102U;
        Rsmu.SMU_MSG_EnableOcMode = 23U;
        Rsmu.SMU_MSG_DisableOcMode = 24U;
        Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 25U;
        Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 26U;
        Rsmu.SMU_MSG_SetOverclockCpuVid = 27U;
        Rsmu.SMU_MSG_SetPPTLimit = 51U;
        Rsmu.SMU_MSG_SetHTCLimit = 55U;
        Rsmu.SMU_MSG_SetTDCVDDLimit = 56U;
        Rsmu.SMU_MSG_SetTDCSOCLimit = 57U;
        Rsmu.SMU_MSG_SetEDCVDDLimit = 58U;
        Rsmu.SMU_MSG_SetEDCSOCLimit = 59U;
        Rsmu.SMU_MSG_SetPBOScalar = 63U;
        Rsmu.SMU_MSG_GetPBOScalar = 15U;
        Mp1Smu.SMU_ADDR_MSG = 61932840U;
        Mp1Smu.SMU_ADDR_RSP = 61932900U;
        Mp1Smu.SMU_ADDR_ARG = 61933976U;
        Mp1Smu.SMU_MSG_EnableOcMode = 47U;
        Mp1Smu.SMU_MSG_DisableOcMode = 48U;
        Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 50U;
        Mp1Smu.SMU_MSG_SetOverclockCpuVid = 51U;
        Mp1Smu.SMU_MSG_SetHTCLimit = 62U;
        Mp1Smu.SMU_MSG_SetPBOScalar = 73U;
    }
}