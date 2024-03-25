namespace Saku_Overclock.SMUEngine;

public sealed class HSMPMailbox : Mailbox
{
    private uint GetHighestSupportedId()
    {
        switch (InterfaceVersion)
        {
            case 1:
                return 17;
            case 2:
                return 18;
            case 3:
                return 20;
            case 4:
                return 21;
            case 5:
                return 34;
            default:
                return 34;
        }
    }
    [Obsolete("Obsolete")]
    public void Init(Cpu cpu)
    {
        var args = Utils.MakeCmdArgs(maxArgs: MAX_ARGS);
        if (cpu.Smu.SendSmuCommand(this, GetInterfaceVersion, ref args) != SMU.Status.OK)
        {
            return;
        }

        InterfaceVersion = args[0];
        HighestSupportedFunction = GetHighestSupportedId();
    }
    public uint InterfaceVersion;
    public uint HighestSupportedFunction;

    public HSMPMailbox(int maxArgs = 8)
        : base(maxArgs)
    {
    }

    public bool IsSupported => InterfaceVersion > 0U;

    public uint GetInterfaceVersion { get; set; } = 3;

    public uint ReadSocketPower { get; set; } = 4;

    public uint WriteSocketPowerLimit { get; set; } = 5;

    public uint ReadSocketPowerLimit { get; set; } = 6;

    public uint ReadMaxSocketPowerLimit { get; set; } = 7;

    public uint WriteBoostLimit { get; set; } = 8;

    public uint WriteBoostLimitAllCores { get; set; } = 9;

    public uint ReadBoostLimit { get; set; } = 10;

    public uint ReadProchotStatus { get; set; } = 11;

    public uint SetXgmiLinkWidthRange { get; set; } = 12;

    public uint APBDisable { get; set; } = 13;

    public uint APBEnable { get; set; } = 14;

    public uint ReadCurrentFclkMemclk { get; set; } = 15;

    public uint ReadCclkFrequencyLimit { get; set; } = 16;

    public uint ReadSocketC0Residency { get; set; } = 17;

    public uint SetLclkDpmLevelRange { get; set; } = 18;

    public uint GetLclkDpmLevelRange { get; set; } = 19;

    public uint GetMaxDDRBandwidthAndUtilization { get; set; } = 20;

    public uint GetDIMMTempRangeAndRefreshRate { get; set; } = 22;

    public uint GetDIMMPowerConsumption { get; set; } = 23;

    public uint GetDIMMThermalSensor { get; set; } = 24;

    public uint PwrCurrentActiveFreqLimitSocket { get; set; } = 25;

    public uint PwrCurrentActiveFreqLimitCore { get; set; } = 26;

    public uint PwrSviTelemetryAllRails { get; set; } = 27;

    public uint GetSocketFreqRange { get; set; } = 28;

    public uint GetCurrentIoBandwidth { get; set; } = 29;

    public uint GetCurrentXgmiBandwidth { get; set; } = 14;

    public uint SetGMI3LinkWidthRange { get; set; } = 31;

    public uint ControlPcieLinkRate { get; set; } = 32;

    public uint PwrEfficiencyModeSelection { get; set; } = 33;

    public uint SetDfPstateRange { get; set; } = 34;
}