using Saku_Overclock.SMUEngine;
namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal static class GetSMUStatusHelpers
{

    public static string GetByType(SMU.Status type)
    {
        string str;
        return GetSMUStatus.status.TryGetValue(type, out str) ? str : "Unknown Status";
    }
}