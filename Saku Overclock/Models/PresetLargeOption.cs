namespace Saku_Overclock.Models;

public class PresetLargeOption<T>(bool[] isEnabled, T value)
{
    public bool[] IsEnabled { get; set; } = isEnabled;
    public T Value { get; set; } = value;

    public PresetLargeOption(T value) : this([], value) { }
}