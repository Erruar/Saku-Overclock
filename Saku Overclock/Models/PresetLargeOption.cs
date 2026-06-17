using System.Text.Json.Serialization;

namespace Saku_Overclock.Models;

public class PresetLargeOption<T>
{
    public bool[] IsEnabled { get; set; } = null!;
    public T Value { get; set; } = default!;

    [JsonConstructor]
    public PresetLargeOption() { }

    public PresetLargeOption(bool[] isEnabled, T value)
    {
        IsEnabled = isEnabled;
        Value = value;
    }
    public PresetLargeOption(T value) : this([], value) { }
}