namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal static class Constants
{
    internal const string VENDOR_AMD = "AuthenticAMD";
    internal const string VENDOR_HYGON = "HygonGenuine";
    internal const uint F17H_M01H_SVI = 368640;
    internal const uint F17H_M60H_SVI = 454656;
    internal const uint F17H_M01H_SVI_TEL_PLANE0 = 368652;
    internal const uint F17H_M01H_SVI_TEL_PLANE1 = 368656;
    internal const uint F17H_M30H_SVI_TEL_PLANE0 = 368660;
    internal const uint F17H_M30H_SVI_TEL_PLANE1 = 368656;
    internal const uint F17H_M60H_SVI_TEL_PLANE0 = 454712;
    internal const uint F17H_M60H_SVI_TEL_PLANE1 = 454716;
    internal const uint F17H_M70H_SVI_TEL_PLANE0 = 368656;
    internal const uint F17H_M70H_SVI_TEL_PLANE1 = 368652;
    internal const uint F19H_M01H_SVI_TEL_PLANE0 = 368656;
    internal const uint F19H_M01H_SVI_TEL_PLANE1 = 368660;
    internal const uint F19H_M21H_SVI_TEL_PLANE0 = 368656;
    internal const uint F19H_M21H_SVI_TEL_PLANE1 = 368652;
    internal const uint F17H_M70H_CCD_TEMP = 366932;
    internal const uint THM_CUR_TEMP = 366592;
    internal const uint THM_CUR_TEMP_RANGE_SEL_MASK = 524288;
    internal const int DEFAULT_MAILBOX_ARGS = 6;
    internal const int HSMP_MAILBOX_ARGS = 8;
    internal const float PBO_SCALAR_MIN = 0.0f;
    internal const float PBO_SCALAR_MAX = 10f;
    internal const float PBO_SCALAR_DEFAULT = 1f;
    internal static readonly string[] MISIDENTIFIED_DALI_APU = new string[12]
    {
        "Athlon Silver 3050GE",
        "Athlon Silver 3050U",
        "3015e",
        "3020e",
        "Athlon Gold 3150U",
        "Athlon Silver 3050e",
        "Ryzen 3 3250U",
        "Athlon 3000G",
        "Athlon 300GE",
        "Athlon 300U",
        "Athlon 320GE",
        "Ryzen 3 3200U"
    };
}