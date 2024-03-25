namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class AodData
{
    public int SMTEn
    {
        get; set;
    }

    public int MemClk
    {
        get; set;
    }

    public int Tcl
    {
        get; set;
    }

    public int Trcd
    {
        get; set;
    }

    public int Trp
    {
        get; set;
    }

    public int Tras
    {
        get; set;
    }

    public int Trc
    {
        get; set;
    }

    public int Twr
    {
        get; set;
    }

    public int Trfc
    {
        get; set;
    }

    public int Trfc2
    {
        get; set;
    }

    public int Trfcsb
    {
        get; set;
    }

    public int Trtp
    {
        get; set;
    }

    public int TrrdL
    {
        get; set;
    }

    public int TrrdS
    {
        get; set;
    }

    public int Tfaw
    {
        get; set;
    }

    public int TwtrL
    {
        get; set;
    }

    public int TwtrS
    {
        get; set;
    }

    public int TrdrdScL
    {
        get; set;
    }

    public int TrdrdSc
    {
        get; set;
    }

    public int TrdrdSd
    {
        get; set;
    }

    public int TrdrdDd
    {
        get; set;
    }

    public int TwrwrScL
    {
        get; set;
    }

    public int TwrwrSc
    {
        get; set;
    }

    public int TwrwrSd
    {
        get; set;
    }

    public int TwrwrDd
    {
        get; set;
    }

    public int Twrrd
    {
        get; set;
    }

    public int Trdwr
    {
        get; set;
    }

    public int CadBusDrvStren
    {
        get; set;
    }

    public int ProcDataDrvStren
    {
        get; set;
    }

    public int ProcODT
    {
        get; set;
    }

    public int DramDataDrvStren
    {
        get; set;
    }

    public int RttNomWr
    {
        get; set;
    }

    public int RttNomRd
    {
        get; set;
    }

    public int RttWr
    {
        get; set;
    }

    public int RttPark
    {
        get; set;
    }

    public int RttParkDqs
    {
        get; set;
    }

    public int MemVddio
    {
        get; set;
    }

    public int MemVddq
    {
        get; set;
    }

    public int MemVpp
    {
        get; set;
    }

    public static AodData? CreateFromByteArray(
        byte[]? byteArray,
        Dictionary<string, int> fieldDictionary)
    {
        var fromByteArray = new AodData();
        foreach (var field in fieldDictionary)
        {
            var key = field.Key;
            var startIndex = field.Value;
            var int32 = BitConverter.ToInt32(byteArray, startIndex);
            typeof(AodData).GetProperty(key)?.SetValue(fromByteArray, int32, null);
        }
        return fromByteArray;
    }
}