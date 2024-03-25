using System.Runtime.InteropServices;
using System.Text;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

public static class Utils
{
    public static bool Is64Bit => OpenHardwareMonitor.Hardware.OperatingSystem.Is64BitOperatingSystem;

    public static uint SetBits(uint val, int offset, int n, uint newVal)
    {
        return (uint)((int)val & ~((1 << n) - 1 << offset) | (int)newVal << offset);
    }

    public static uint GetBits(uint val, int offset, int n) => val >> offset & (uint)~(-1 << n);

    public static uint CountSetBits(uint v)
    {
        uint num = 0;
        for (; v > 0U; v >>= 1)
        {
            if (((int)v & 1) == 1)
            {
                ++num;
            }
        }
        return num;
    }

    public static string GetStringPart(uint val) => val == 0U ? "" : Convert.ToChar(val).ToString();

    public static string IntToStr(uint val)
    {
        var val1 = (int)val & byte.MaxValue;
        var val2 = val >> 8 & byte.MaxValue;
        var val3 = val >> 16 & byte.MaxValue;
        var val4 = val >> 24 & byte.MaxValue;
        return GetStringPart((uint)val1) + GetStringPart(val2) + GetStringPart(val3) + GetStringPart(val4);
    }

    public static double VidToVoltage(uint vid) => 1.55 - vid * (1.0 / 160.0);

    public static double VidToVoltageSVI3(uint vid) => 0.245 + vid * 0.005;

    private static bool CheckAllZero<T>(ref T[] typedArray)
    {
        if (typedArray == null)
        {
            return true;
        }

        foreach (var obj in typedArray)
        {
            if (Convert.ToUInt32(obj) != 0U)
            {
                return false;
            }
        }
        return true;
    }

    public static bool AllZero(byte[] arr) => CheckAllZero(ref arr);

    public static bool AllZero(int[] arr) => CheckAllZero(ref arr);

    public static bool AllZero(uint[] arr) => CheckAllZero(ref arr);

    public static bool AllZero(float[] arr) => CheckAllZero(ref arr);

    public static uint[] MakeCmdArgs(uint[] args, int maxArgs = 6)
    {
        var numArray = new uint[maxArgs];
        var num = Math.Min(maxArgs, args.Length);
        for (var index = 0; index < num; ++index)
        {
            numArray[index] = args[index];
        }

        return numArray;
    }

    public static uint[] MakeCmdArgs(uint arg = 0, int maxArgs = 6)
    {
        return MakeCmdArgs(new uint[1] { arg }, maxArgs);
    }

    public static uint MakePsmMarginArg(int margin)
    {
        return Convert.ToUInt32((margin < 0 ? 1048576 : 0) + margin) & ushort.MaxValue;
    }

    public static T ByteArrayToStructure<T>(byte[]? byteArray) where T : new()
    {
        var gcHandle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
        try
        {
            return (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
        }
        finally
        {
            gcHandle.Free();
        }
    }

    public static uint ReverseBytes(uint value)
    {
        return (uint)(((int)value & byte.MaxValue) << 24 | ((int)value & 65280) << 8) | (value & 16711680U) >> 8 | (value & 4278190080U) >> 24;
    }

    public static string GetStringFromBytes(uint value)
    {
        return Encoding.ASCII.GetString(BitConverter.GetBytes(value)).Replace("\0", " ");
    }

    public static string GetStringFromBytes(ulong value)
    {
        return Encoding.ASCII.GetString(BitConverter.GetBytes(value)).Replace("\0", " ");
    }

    public static string GetStringFromBytes(byte[] value)
    {
        return Encoding.ASCII.GetString(value).Replace("\0", " ");
    }

    public static int FindSequenceAsync(byte[]? array, int start, byte[] sequence)
    {
        var num1 = array!.Length - sequence.Length;
        var num2 = sequence[0];
        label_8:
        for (; start <= num1 && start + sequence.Length <= array.Length; ++start)
        {
            if (array[start] == num2)
            { 
                for (var index = 1; index != sequence.Length; ++index)
                {
                    if (array[start + index] != sequence[index])
                    {
                        goto label_8;
                    }
                }
                return start;
            }
        }
        return -1;
    }

    public static bool ArrayMembersEqual(float[] array1, float[] array2, int numElements)
    {
        if (array1.Length < numElements || array2.Length < numElements)
        {
            throw new ArgumentException("Arrays are not long enough to compare the specified number of elements.");
        }

        for (var index = 0; index < numElements; ++index)
        {
            if (array1[index] != (double)array2[index])
            {
                return false;
            }
        }
        return true;
    }

    public static bool PartialStringMatch(string str, string[] arr)
    {
        var flag = false;
        for (var index = 0; index < arr.Length; ++index)
        {
            if (str.Contains(arr[index]))
            {
                flag = true;
                break;
            }
        }
        return flag;
    }
}