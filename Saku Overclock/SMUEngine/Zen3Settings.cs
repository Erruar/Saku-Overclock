namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class Zen3Settings : Zen2Settings
{
    public Zen3Settings()
    {
        SMU_TYPE = SmuType.TYPE_CPU3;
        Rsmu.SMU_MSG_SetDldoPsmMargin = 10U;
        Rsmu.SMU_MSG_SetAllDldoPsmMargin = 11U;
        Rsmu.SMU_MSG_GetDldoPsmMargin = 124U;
        Mp1Smu.SMU_MSG_SetDldoPsmMargin = 53U;
        Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 54U;
        Mp1Smu.SMU_MSG_GetDldoPsmMargin = 72U;
    }
}