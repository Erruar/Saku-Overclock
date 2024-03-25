namespace Saku_Overclock.SMUEngine;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
public class AOD
{
    internal readonly IOModule io;
    internal readonly ACPI acpi;
    internal readonly Cpu.CodeName codeName;
    public AodTable Table;

    private static string GetByKey(Dictionary<int, string> dict, int key)
    {
        return !dict.TryGetValue(key, out var str) ? "N/A" : str;
    }

    public static string GetProcODTString(int key)
    {
        return GetByKey(AodDictionaries.ProcOdtDict, key);
    }

    public static string GetProcDataDrvStrenString(int key)
    {
        return GetByKey(AodDictionaries.ProcOdtDict, key);
    }

    public static string GetDramDataDrvStrenString(int key)
    {
        return GetByKey(AodDictionaries.DramDataDrvStrenDict, key);
    }

    public static string GetCadBusDrvStrenString(int key)
    {
        return GetByKey(AodDictionaries.CadBusDrvStrenDict, key);
    }

    public static string GetRttString(int key) => GetByKey(AodDictionaries.RttDict, key);

    public AOD(IOModule io, Cpu.CodeName codeName)
    {
        this.io = io;
        this.codeName = codeName;
        acpi = new ACPI(io);
        Table = new AodTable();
        Init();
    }

    private static void Init()
    { 
        
    }

    private static Dictionary<string, int> GetAodDataDictionary(Cpu.CodeName codeName)
    {
        switch (codeName)
        {
            case Cpu.CodeName.Genoa:
            case Cpu.CodeName.StormPeak:
                return AodDictionaries.AodDataStormPeakDictionary;
            default:
                return AodDictionaries.AodDataDefaultDictionary;
        }
    }

    public bool Refresh()
    {
        try
        {
            Table.RawAodTable = io.ReadMemory(new IntPtr(Table.BaseAddress), Table.Length);
            Table.Data = AodData.CreateFromByteArray(Table.RawAodTable, GetAodDataDictionary(codeName));
            return true;
        }
        catch
        {
            // ignored
        }

        return false;
    } 

    [Serializable]
    public class AodTable
    {
        public readonly uint Signature;
        public ulong OemTableId;
        public uint BaseAddress;
        public int Length;
        public ACPI.ACPITable? AcpiTable;
        public AodData? Data;
        public byte[]? RawAodTable;

        public AodTable()
        {
            Signature = ACPI.Signature("SSDT");
            OemTableId = ACPI.SignatureUL("AOD     ");
        }
    }
}