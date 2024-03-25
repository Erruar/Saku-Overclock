namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class BristolRidgeSettings : SMU
{
    public BristolRidgeSettings()
    {
        SMU_TYPE = SmuType.TYPE_CPU9;
        SMU_OFFSET_ADDR = 184U;
        SMU_OFFSET_DATA = 188U;
        Rsmu.SMU_ADDR_MSG = 318767104U;
        Rsmu.SMU_ADDR_RSP = 318767120U;
        Rsmu.SMU_ADDR_ARG = 318767136U;
    }
}