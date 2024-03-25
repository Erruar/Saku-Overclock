using System.ComponentModel;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

internal static class InternalEventArgsCache
{
    internal static PropertyChangedEventArgs FCLK = new PropertyChangedEventArgs(nameof(FCLK));
    internal static PropertyChangedEventArgs MCLK = new PropertyChangedEventArgs(nameof(MCLK));
    internal static PropertyChangedEventArgs UCLK = new PropertyChangedEventArgs(nameof(UCLK));
    internal static PropertyChangedEventArgs VDDCR_SOC = new PropertyChangedEventArgs(nameof(VDDCR_SOC));
    internal static PropertyChangedEventArgs CLDO_VDDP = new PropertyChangedEventArgs(nameof(CLDO_VDDP));
    internal static PropertyChangedEventArgs CLDO_VDDG_IOD = new PropertyChangedEventArgs(nameof(CLDO_VDDG_IOD));
    internal static PropertyChangedEventArgs CLDO_VDDG_CCD = new PropertyChangedEventArgs(nameof(CLDO_VDDG_CCD));
    internal static PropertyChangedEventArgs VDD_MISC = new PropertyChangedEventArgs(nameof(VDD_MISC));
}