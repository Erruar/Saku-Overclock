namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal readonly struct IoControlCode
{
    private enum Method : uint
    {
        Buffered, // ReSharper disable once UnusedMember.Local
        InDirect, // ReSharper disable once UnusedMember.Local
        OutDirect, // ReSharper disable once UnusedMember.Local
        Neither
    }

    public enum Access : uint
    {
        Any,
        Read,
        Write
    }

    // ReSharper disable once NotAccessedField.Local
    private readonly uint code;

    public IoControlCode(uint deviceType, uint function, Access access)
        : this(deviceType, function, Method.Buffered, access)
    {
    }

    private IoControlCode(uint deviceType, uint function, Method method, Access access)
    {
        code = (deviceType << 16) | ((uint)access << 14) | (function << 2) | (uint)method;
    }
}