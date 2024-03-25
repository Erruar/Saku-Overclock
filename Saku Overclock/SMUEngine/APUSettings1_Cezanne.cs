namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class APUSettings1_Cezanne : APUSettings1
{
    public APUSettings1_Cezanne()
    {
        Rsmu.SMU_MSG_SetDldoPsmMargin = 82U;
        Rsmu.SMU_MSG_SetAllDldoPsmMargin = 177U;
        Rsmu.SMU_MSG_GetDldoPsmMargin = 195U;
        Rsmu.SMU_MSG_SetGpuPsmMargin = 83U;
        Rsmu.SMU_MSG_GetGpuPsmMargin = 198U;
    }
}