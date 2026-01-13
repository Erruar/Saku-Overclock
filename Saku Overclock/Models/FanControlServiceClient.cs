using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Saku_Overclock.Models;

public class FanControlServiceClient(
    Binding binding,
    EndpointAddress address) : ClientBase<IFanControlService>(binding, address)
{
    public void Start(bool readOnly) =>
        Channel.Start(readOnly);

    public void Stop() =>
        Channel.Stop();

    public FanControlInfo GetFanControlInfo() =>
        Channel.GetFanControlInfo();

    public void SetTargetFanSpeed(float value, int fanIndex) =>
        Channel.SetTargetFanSpeed(value, fanIndex);

    public void SetConfig(string uniqueConfigId) =>
        Channel.SetConfig(uniqueConfigId);

    public string[] GetConfigNames() =>
        Channel.GetConfigNames();

    public string[] GetRecommendedConfigs() =>
        Channel.GetRecommendedConfigs();
}

[ServiceContract(ConfigurationName = "NbfcService.IFanControlService")]
public interface IFanControlService
{
    [OperationContract]
    void Start(bool readOnly);

    [OperationContract]
    void Stop();

    [OperationContract]
    FanControlInfo GetFanControlInfo();

    [OperationContract]
    void SetTargetFanSpeed(float value, int fanIndex);

    [OperationContract]
    void SetConfig(string uniqueConfigId);

    [OperationContract]
    string[] GetConfigNames();

    [OperationContract]
    string[] GetRecommendedConfigs();
}

[DataContract(
    Name = "FanStatus",
    Namespace = "http://schemas.datacontract.org/2004/07/StagWare.FanControl.Service")]
public sealed class FanStatus : NotifyObject
{
    private bool _autoControlEnabled;
    private bool _criticalModeEnabled;
    private int _fanSpeedSteps;
    private float _currentFanSpeed;
    private float _targetFanSpeed;
    private string? _fanDisplayName;

    [DataMember]
    public bool AutoControlEnabled
    {
        get => _autoControlEnabled;
        set => Set(ref _autoControlEnabled, value);
    }

    [DataMember]
    public bool CriticalModeEnabled
    {
        get => _criticalModeEnabled;
        set => Set(ref _criticalModeEnabled, value);
    }

    [DataMember]
    public int FanSpeedSteps
    {
        get => _fanSpeedSteps;
        set => Set(ref _fanSpeedSteps, value);
    }

    [DataMember]
    public float CurrentFanSpeed
    {
        get => _currentFanSpeed;
        set => Set(ref _currentFanSpeed, value);
    }

    [DataMember]
    public float TargetFanSpeed
    {
        get => _targetFanSpeed;
        set => Set(ref _targetFanSpeed, value);
    }

    [DataMember]
    public string? FanDisplayName
    {
        get => _fanDisplayName;
        set => Set(ref _fanDisplayName, value);
    }
}

[DataContract(
    Name = "FanControlInfo",
    Namespace = "http://schemas.datacontract.org/2004/07/StagWare.FanControl.Service")]
public sealed class FanControlInfo : NotifyObject
{
    private bool _enabled;
    private bool _readOnly;
    private int _temperature;
    private string? _selectedConfig;
    private string? _temperatureSourceDisplayName;
    private FanStatus[]? _fanStatus;

    [DataMember]
    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    [DataMember]
    public bool ReadOnly
    {
        get => _readOnly;
        set => Set(ref _readOnly, value);
    }

    [DataMember]
    public int Temperature
    {
        get => _temperature;
        set => Set(ref _temperature, value);
    }

    [DataMember]
    public string? SelectedConfig
    {
        get => _selectedConfig;
        set => Set(ref _selectedConfig, value);
    }

    [DataMember]
    public string? TemperatureSourceDisplayName
    {
        get => _temperatureSourceDisplayName;
        set => Set(ref _temperatureSourceDisplayName, value);
    }

    [DataMember]
    public FanStatus[]? FanStatus
    {
        get => _fanStatus;
        set => Set(ref _fanStatus, value);
    }
}

[DataContract]
public abstract class NotifyObject : INotifyPropertyChanged, IExtensibleDataObject
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    ExtensionDataObject? IExtensibleDataObject.ExtensionData
    {
        get;
        set;
    }
}