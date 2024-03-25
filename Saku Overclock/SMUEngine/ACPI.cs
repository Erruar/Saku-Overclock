using System.Runtime.InteropServices;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;

public class ACPI
{
    internal const uint RSDP_REGION_BASE_ADDRESS = 917504;
    internal const int RSDP_REGION_LENGTH = 131071;
    private readonly IOModule io;

    public ACPI(IOModule io) => this.io = io ?? throw new ArgumentNullException(nameof(io));

    public static ParsedSDTHeader ParseRawHeader(SDTHeader rawHeader)
    {
        return new ParsedSDTHeader
        {
            Signature = Utils.GetStringFromBytes(rawHeader.Signature),
            Length = rawHeader.Length,
            Revision = rawHeader.Revision,
            Checksum = rawHeader.Checksum,
            OEMID = Utils.GetStringFromBytes(rawHeader.OEMID),
            OEMTableID = Utils.GetStringFromBytes(rawHeader.OEMTableID),
            OEMRevision = rawHeader.OEMRevision,
            CreatorID = Utils.GetStringFromBytes(rawHeader.CreatorID),
            CreatorRevision = rawHeader.CreatorRevision
        };
    }

    public static uint Signature(string ascii)
    {
        uint num1 = 0;
        var num2 = Math.Min(ascii.Length, 4);
        for (var index = 0; index < num2; ++index)
        {
            num1 |= (uint)ascii[index] << index * 8;
        }

        return num1;
    }

    public static ulong SignatureUL(string ascii)
    {
        ulong num1 = 0;
        var num2 = Math.Min(ascii.Length, 8);
        for (var index = 0; index < num2; ++index)
        {
            num1 |= (ulong)ascii[index] << index * 8;
        }

        return num1;
    }

    public static byte[] ByteSignature(string ascii)
    {
        return BitConverter.GetBytes(Signature(ascii));
    }

    public static byte[] ByteSignatureUL(string ascii)
    {
        return BitConverter.GetBytes(SignatureUL(ascii));
    }

    public T GetHeader<T>(uint address, int length = 36) where T : new()
    {
        return Utils.ByteArrayToStructure<T>(io.ReadMemory(new IntPtr(address), length));
    }

    public RSDP GetRsdp()
    {
        var sequence = Utils.FindSequenceAsync(io.ReadMemory(new IntPtr(917504L), 131071), 0, ByteSignatureUL("RSD PTR "));
        return sequence >= 0 ? Utils.ByteArrayToStructure<RSDP>(io.ReadMemory(new IntPtr(917504L + sequence), 36)) : throw new SystemException("ACPI: Could not find RSDP signature");
    }

    public RSDT GetRSDT()
    {
        var rsdp = GetRsdp();
        var header = GetHeader<SDTHeader>(rsdp.RsdtAddress);
        var src = io.ReadMemory(new IntPtr(rsdp.RsdtAddress), (int)header.Length);
        var gcHandle = GCHandle.Alloc(src, GCHandleType.Pinned);
        RSDT rsdt;
        try
        {
            var srcOffset = Marshal.SizeOf(header);
            var count = (int)header.Length - srcOffset;
            rsdt = new RSDT
            {
                Header = header,
                Data = new uint[count]
            };
            Buffer.BlockCopy(src, srcOffset, rsdt.Data, 0, count);
        }
        finally
        {
            gcHandle.Free();
        }
        return rsdt;
    }

    public static ACPITable ParseSdtTable(byte[] rawTable)
    {
        var gcHandle = GCHandle.Alloc(rawTable, GCHandleType.Pinned);
        ACPITable sdtTable;
        try
        {
            var structure = (SDTHeader)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SDTHeader))!;
            var srcOffset = Marshal.SizeOf(structure);
            var count = (int)structure.Length - srcOffset;
            sdtTable = new ACPITable
            {
                RawHeader = structure,
                Header = ParseRawHeader(structure),
                Data = new byte[count]
            };
            Buffer.BlockCopy(rawTable, srcOffset, sdtTable.Data, 0, count);
        }
        finally
        {
            gcHandle.Free();
        }
        return sdtTable;
    }

    public static class TableSignature
    {
        public const string RSDP = "RSD PTR ";
        public const string RSDT = "RSDT";
        public const string XSDT = "XSDT";
        public const string SSDT = "SSDT";
        public const string AOD_ = "AOD     ";
        public const string AAOD = "AMD AOD";
        public const string AODE = "AODE";
        public const string AODT = "AODT";
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 36, Pack = 1)]
    public struct RSDP
    {
        public ulong Signature;
        public byte Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] OEMID;
        public byte Revision;
        public uint RsdtAddress;
        public uint Length;
        public ulong XsdtAddress;
        public byte ExtendedChecksum;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 36, Pack = 1)]
    public struct SDTHeader
    {
        public uint Signature;
        public uint Length;
        public byte Revision;
        public byte Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] OEMID;
        public ulong OEMTableID;
        public uint OEMRevision;
        public uint CreatorID;
        public uint CreatorRevision;
    }

    [Serializable]
    public struct ParsedSDTHeader
    {
        public string Signature;
        public uint Length;
        public byte Revision;
        public byte Checksum;
        public string OEMID;
        public string OEMTableID;
        public uint OEMRevision;
        public string CreatorID;
        public uint CreatorRevision;
    }

    [Serializable]
    public struct ACPITable
    {
        public SDTHeader RawHeader;
        public ParsedSDTHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] Data;
    }

    [Serializable]
    public struct RSDT
    {
        public SDTHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] Data;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct FADT
    {
        [FieldOffset(0)]
        public SDTHeader Header;
        [FieldOffset(36)]
        public uint FIRMWARE_CTRL;
        [FieldOffset(40)]
        public uint DSDT;
        [FieldOffset(132)]
        public ulong X_FIRMWARE_CTRL;
        [FieldOffset(140)]
        public ulong X_DSDT;
    }

    public enum AddressSpace : byte
    {
        SystemMemory,
        SystemIo,
        PciConfigSpace,
        EmbeddedController,
        SMBus,
        SystemCmos,
        PciBarTarget,
        Ipmi,
        GeneralIo,
        GenericSerialBus,
        PlatformCommunicationsChannel,
        FunctionalFixedHardware,
        OemDefined,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 1)]
    public struct OperationRegion
    {
        public uint RegionName;
        public AddressSpace RegionSpace;
        public byte _unknown1;
        public uint Offset;
        public byte _unknown2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Length;
        public byte _unknown3;
        public byte _unknown4;
        public byte _unknown5;
    }
}