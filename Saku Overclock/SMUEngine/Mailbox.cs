namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class Mailbox
{
    public Mailbox(int maxArgs = 6)
    {
        MAX_ARGS = maxArgs;
    }

    public int MAX_ARGS
    {
        get; protected set;
    }

    public uint SMU_ADDR_MSG { get; set; }

    public uint SMU_ADDR_RSP { get; set; }

    public uint SMU_ADDR_ARG { get; set; }

    public uint SMU_MSG_TestMessage { get; } = 1;

    public uint SMU_MSG_GetSmuVersion { get; } = 2;
}