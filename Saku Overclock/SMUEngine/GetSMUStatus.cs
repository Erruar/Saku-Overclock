namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal static class GetSMUStatus
{
    public static readonly Dictionary<SMU.Status, string> status = new()
    {
        {
            SMU.Status.OK,
            "OK"
        },
        {
            SMU.Status.FAILED,
            "Failed"
        },
        {
            SMU.Status.UNKNOWN_CMD,
            "Unknown Command"
        },
        {
            SMU.Status.CMD_REJECTED_PREREQ,
            "CMD Rejected Prereq"
        },
        {
            SMU.Status.CMD_REJECTED_BUSY,
            "CMD Rejected Busy"
        }
    };
}